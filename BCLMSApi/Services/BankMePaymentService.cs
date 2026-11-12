using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCLMSApi.Data;
using BCLMSApi.Models;

namespace BCLMSApi.Services;

public sealed class BankMeOptions
{
    public string AuthUrl { get; set; } = "https://www.uat.e-tshwane.co.za/bl-bankme/payment/secure/auth";
    public string IframeUrl { get; set; } = "https://www.uat.e-tshwane.co.za/bl-bankme/payment/index.html";
    public string Origin { get; set; } = "https://www.uat.e-tshwane.co.za";
    public string? ThemeUrl { get; set; }
    public string? AuthKeyBase64 { get; set; }
    public string? WebhookSecret { get; set; }
}

public sealed record BankMeTokenResponse(string Token, string IframeUrl, string Origin, string? ThemeUrl);

public sealed class BankMePaymentService(HttpClient http, IConfiguration configuration, InvoiceRepository invoices, ILogger<BankMePaymentService> logger)
{
    private readonly BankMeOptions options = configuration.GetSection("BankMe").Get<BankMeOptions>() ?? new();

    public string IframeUrl => options.IframeUrl;
    public string Origin => options.Origin;
    public string? ThemeUrl => options.ThemeUrl;

    public async Task<BankMeTokenResponse> CreateTokenAsync(InternalApplicationDetailResponse application, InvoiceData invoice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.AuthKeyBase64))
            throw new InvalidOperationException("BankME AuthKeyBase64 is not configured.");
        if (invoice.Lines.Count == 0 || invoice.PaymentStatus.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This invoice is not available for payment.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var groupReference = $"{invoice.PaymentReference}-{now}";
        var uniqueReference = Guid.NewGuid().ToString();
        var total = invoice.Lines.Sum(line => line.Amount);
        var items = invoice.Lines.Select(line => new
        {
            amount = line.Amount,
            currency = "ZAR",
            name = string.IsNullOrWhiteSpace(application.LicenceType) ? line.Description : $"{application.LicenceType} - {line.Description}",
            reference = invoice.PaymentReference,
            mr = invoice.PaymentReference,
            dr = invoice.PaymentReference,
            cr = groupReference,
            paymentGUID = Guid.NewGuid().ToString(),
            dueDate = now,
        }).ToArray();
        var authRecord = new
        {
            groupReference,
            uniqueReference,
            onlyCard = true,
            user = new
            {
                tenantUserId = application.IdOrPassportNumber,
                idNumber = application.IdOrPassportNumber,
                username = application.ApplicantName,
                initials = Initials(application.ApplicantName),
                surname = Surname(application.ApplicantName),
                passport = application.IdOrPassportNumber,
                country = "South Africa",
            },
            items,
        };

        var body = Encrypt(JsonSerializer.Serialize(authRecord), options.AuthKeyBase64);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.AuthUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        using var response = await http.SendAsync(request, cancellationToken);
        var token = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("BankME token request failed with status {StatusCode}.", response.StatusCode);
            throw new InvalidOperationException("The payment service could not start this payment.");
        }

        await invoices.SaveBankMeAttemptAsync(application.ApplicationId, groupReference, uniqueReference, total);
        return new BankMeTokenResponse(token, IframeUrl, Origin, ThemeUrl);
    }

    public bool IsValidWebhook(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(options.WebhookSecret) || string.IsNullOrWhiteSpace(signature)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.WebhookSecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public Task CompleteWebhookAsync(string groupReference, string paymentUuid, string status, decimal? totalAmount) => invoices.CompleteBankMeAttemptAsync(groupReference, paymentUuid, status, totalAmount);

    private static string Encrypt(string json, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        if (key.Length != 32) throw new InvalidOperationException("BankME AuthKeyBase64 must decode to 32 bytes.");
        var iv = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(json);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, plain, cipher, tag);
        return Convert.ToBase64String(iv.Concat(cipher).Concat(tag).ToArray());
    }

    private static string Initials(string name) => string.Join(string.Empty, name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0])).ToUpperInvariant();
    private static string Surname(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? name;
}

public sealed record BankMeWebhookPayload(string? GroupReference, string? PaymentUuid, string? Status, decimal? TotalAmount);
