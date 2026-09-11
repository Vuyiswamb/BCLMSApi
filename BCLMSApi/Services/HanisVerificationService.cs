using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BCLMSApi.Services;

public interface IHanisVerificationService
{
    Task<string> VerifyAsync(string idNumber, string applicantName, string username);
    Task<HanisVerificationService.HanisResult> LookupAsync(string idNumber, string applicantName, string username);
}

public sealed class HanisVerificationService(HttpClient client, ISystemSettingsService settings,
    ILogger<HanisVerificationService> logger) : IHanisVerificationService
{
    public async Task<string> VerifyAsync(string idNumber, string applicantName, string username)
        => ValidateResult(await LookupAsync(idNumber, applicantName, username), idNumber.Trim(), applicantName);

    public async Task<HanisResult> LookupAsync(string idNumber, string applicantName, string username)
    {
        idNumber = idNumber.Trim();
        if (!Regex.IsMatch(idNumber, "^[0-9]{13}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Home Affairs requires a 13-digit South African ID. Passport applicants remain pending for manual verification.");

        var hanisSettings = await settings.GetHanisSettingsAsync();
        var key = await settings.GetHanisApiKeyAsync();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(hanisSettings.Username)
            || !Uri.TryCreate(hanisSettings.BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != "https" && !(baseUri.Scheme == "http" && hanisSettings.AllowInsecureQa))
            || !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            throw new ArgumentException("Home Affairs verification is not configured. Contact the system administrator; this step remains pending.");

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/api/lookup"));
                request.Headers.Add("X-Api-Key", key);
                request.Content = JsonContent.Create(new { idNumber, appUserName = hanisSettings.Username,
                    appDepartment = hanisSettings.Department });
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

                var result = await response.Content.ReadFromJsonAsync<HanisResult>(cancellationToken: deadline.Token);
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
        catch (OperationCanceledException)
        {
            throw new ArgumentException("Home Affairs verification timed out. Try again; this step remains pending.");
        }
        catch (HttpRequestException)
        {
            throw new ArgumentException("Cannot connect to Home Affairs. Try again later; this step remains pending.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("Home Affairs returned an invalid response. This step remains pending.");
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
