using System.Net;
using System.Text;
using System.Xml;
using ImageMagick;
using System.Text.RegularExpressions;

namespace BCLMSApi.Services;

public interface IHanisVerificationService
{
    Task<string> VerifyAsync(string idNumber, string applicantName, string username);
    Task<HanisVerificationService.HanisResult> LookupAsync(string idNumber, string applicantName, string username);
}

public sealed class HanisVerificationService(HttpClient client, IConfiguration configuration,
    ILogger<HanisVerificationService> logger, IHostEnvironment environment) : IHanisVerificationService
{
    public async Task<string> VerifyAsync(string idNumber, string applicantName, string username)
        => ValidateResult(await LookupAsync(idNumber, applicantName, username), idNumber.Trim(), applicantName);

    public async Task<HanisResult> LookupAsync(string idNumber, string applicantName, string username)
    {
        idNumber = idNumber.Trim();
        if (!Regex.IsMatch(idNumber, "^[0-9]{13}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Home Affairs requires a 13-digit South African ID. Passport applicants remain pending for manual verification.");

        var options = HanisDirectOptions.Read(configuration, environment.IsDevelopment());

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
                request.Headers.Add("SOAPAction", "\"http://HANIS.org/GetData\"");
                var sentUtc = DateTimeOffset.UtcNow;
                var transactionDate = sentUtc.ToOffset(TimeSpan.FromHours(2)).ToString("ddMMyyyy HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                request.Content = new StringContent(HanisSoapCodec.Request(idNumber, options, sentUtc), Encoding.UTF8, "text/xml");
               
                using var response = await client.SendAsync(request, deadline.Token);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1 << attempt), deadline.Token);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new ArgumentException(response.StatusCode switch
                    {
                        HttpStatusCode.NotFound => "The ID was not found on Home Affairs. The step remains pending.",
                        HttpStatusCode.BadRequest => "Home Affairs could not validate the ID. Check the applicant details and try again.",
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Home Affairs access is not configured correctly. Contact the system administrator.",
                        _ => "Home Affairs is unavailable. Try again later; this step remains pending."
                    });

                HanisResult result;
                try
                {
                    result = HanisSoapCodec.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
                }
                catch (ArgumentException ex) when (ex.Data["HanisErrorCode"] is int code && code == 1056)
                {
                    // The HTTP Date may come from a proxy; use it as evidence, never as an authoritative clock.
                    var providerDate = response.Headers.Date?.ToString("O") ?? "not supplied";
                    logger.LogWarning("HANIS 1056: TransactionDate {TransactionDate}; format ddMMyyyy HHmmss; offset +02:00; sent UTC {SentUtc}; HTTP Date {ProviderDate}; local timezone {LocalTimezone}; trace {TraceId}",
                        transactionDate, sentUtc, providerDate, TimeZoneInfo.Local.Id, System.Diagnostics.Activity.Current?.TraceId.ToString());
                    throw new ArgumentException($"Home Affairs rejected the transaction date (1056). Sent TransactionDate={transactionDate}, format=ddMMyyyy HHmmss, timezone=UTC+02:00; API UTC={sentUtc:O}; provider HTTP Date={providerDate}. Confirm the expected timezone and format with DHA.");
                }
                if (!string.IsNullOrWhiteSpace(result?.HanisTransactionId))
                    logger.LogInformation("HANIS lookup completed for official {Official}; transaction {Transaction}; success {Success}",
                        username, result.HanisTransactionId, result.Success);
                if (result is null || result.Success != true || result.ErrorCode != 0 || result.IdNumber != idNumber)
                    throw new ArgumentException("Home Affairs did not confirm the applicant's ID. This step remains pending.");
                try
                {
                    ValidateResult(result, idNumber, applicantName);
                    result.CanApprove = true;
                    result.VerificationMessage = "The ID and full name match Home Affairs. You can proceed with approval.";
                }
                catch (ArgumentException error)
                {
                    result.CanApprove = false;
                    result.VerificationMessage = error.Message;
                }
                return result;
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new ArgumentException("Home Affairs verification timed out. Try again; this step remains pending.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ArgumentException("Cannot connect to Home Affairs. Try again later; this step remains pending.", ex);
        }
        catch (Exception error) when (error is XmlException or FormatException or MagickException)
        {
            throw new ArgumentException("Home Affairs returned an invalid response. This step remains pending.", error);
        }
        throw new ArgumentException("Home Affairs is unavailable. This step remains pending.");
    }

    internal static string ValidateResult(HanisResult? result, string idNumber, string applicantName)
    {
        if (result is null || result.Success != true || result.ErrorCode != 0 || result.IdNumber != idNumber)
            throw new ArgumentException("Home Affairs did not confirm the applicant's ID. This step remains pending.");
        if (result.IsDeceased == true)
            throw new ArgumentException("Home Affairs records this person as deceased. Review the application; this step remains pending.");
        if (result.IsIdBlocked == true)
            throw new ArgumentException("Home Affairs records this ID as blocked. Review the application; this step remains pending.");
        if (result.IsDeceased != false || result.IsIdBlocked != false || result.IsOnNpr != true)
            throw new ArgumentException("Home Affairs did not confirm an active population-register record. This step remains pending.");
        if (string.IsNullOrWhiteSpace(result.Forenames) || string.IsNullOrWhiteSpace(result.Surname)
            || NormalizeName(applicantName) != NormalizeName($"{result.Forenames} {result.Surname}"))
            throw new ArgumentException("The applicant's full name does not match Home Affairs. Check all given names and surname; this step remains pending.");
        if (string.IsNullOrWhiteSpace(result.HanisTransactionId) || result.HanisTransactionId.Length > 120
            || result.HanisTransactionId.Any(char.IsControl))
            throw new ArgumentException("Home Affairs did not return a valid audit reference. Try again; this step remains pending.");
        return result.HanisTransactionId;
    }

    // Name order, case, punctuation and spacing are ignored; all name tokens must match.
    private static string NormalizeName(string value) => string.Join(" ",
        Regex.Matches(value.Normalize().ToUpperInvariant(), @"[\p{L}\p{M}\p{N}]+")
            .Select(match => match.Value).OrderBy(part => part, StringComparer.Ordinal));

    public sealed class HanisResult
    {
        public DateTime? LookedUpAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public bool IsCached { get; set; }
        public bool? Success { get; set; }
        public int? ErrorCode { get; set; }
        public string? IdNumber { get; set; }
        public string? Forenames { get; set; }
        public string? Surname { get; set; }
        public bool? IsDeceased { get; set; }
        public bool? IsIdBlocked { get; set; }
        public bool? IsOnNpr { get; set; }
        public string? HanisTransactionId { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? BirthCountryCode { get; set; }
        public string? CitizenshipStatus { get; set; }
        public string? MaritalStatus { get; set; }
        public bool? IsOnHanis { get; set; }
        public bool? HasPhoto { get; set; }
        public bool? SmartCardIssued { get; set; }
        public string? IdIssueDate { get; set; }
        public string? DateOfDeath { get; set; }
        public string? PhotoJpegBase64 { get; set; }
        public bool CanApprove { get; set; }
        public string VerificationMessage { get; set; } = string.Empty;
    }
}
