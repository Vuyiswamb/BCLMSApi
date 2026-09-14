using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BCLMSApi.Services;

public class SmsService(HttpClient client, IConfiguration configuration) : ISmsService
{
    public async Task SendSmsAsync(string mobileNumber, string message)
    {
        var number = Regex.Replace(mobileNumber?.Trim() ?? "", "[ ()-]", "");
        if (number.StartsWith("+27", StringComparison.Ordinal)) number = number[1..];
        if (number.StartsWith('0')) number = "27" + number[1..];
        if (!Regex.IsMatch(number, @"\A27[6-8][0-9]{8}\z"))
            throw new ArgumentException("Enter a valid South African cellphone number for SMS invitations.");
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("SMS message is required.");

        var username = configuration["Sms:Vodacom:Username"];
        var password = configuration["Sms:Vodacom:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Vodacom SMS credentials are not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://restapi.gsm.co.za/send/sms"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Use the documented Boolean type and a buffered body with Content-Length.
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                to = number,
                message = message.Trim(),
                ems = "0",
                userref = $"BLS-{Guid.NewGuid():N}"
            }),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try { response = await client.SendAsync(request); }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException("Vodacom SMS request timed out. Delivery status is unknown; check the provider sent logs before retrying.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Vodacom SMS connection failed. Delivery status is unknown; check the provider sent logs before retrying.");
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                // Gateway errors may echo the submitted request. Never return credentials or message content.
                foreach (var secret in new[] { request.Headers.Authorization!.ToString(),
                    request.Headers.Authorization.Parameter!, password, username, number, message.Trim() })
                    detail = detail.Replace(secret, "[redacted]", StringComparison.OrdinalIgnoreCase);
                detail = Regex.Replace(detail, "<[^>]*>", " ");
                detail = Regex.Replace(detail, @"\s+", " ").Trim();
                if (detail.Length > 600) detail = detail[..600] + "...";
                throw new InvalidOperationException($"Vodacom SMS request failed (HTTP {(int)response.StatusCode}). {detail}".Trim());
            }
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = json.RootElement;
                var results = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToArray() : [root];
                if (results.Length == 0) throw InvalidResponse();
                foreach (var result in results)
                {
                    if (result.ValueKind != JsonValueKind.Object) throw InvalidResponse();
                    var error = Read(result, "Error");
                    if (Read(result, "Result") != "1" || error != "0")
                        throw new InvalidOperationException(error switch
                        {
                            "153" => "Vodacom SMS rejected: insufficient credits.",
                            "154" => "Vodacom SMS rejected: invalid or banned cellphone number.",
                            "155" => "Vodacom SMS rejected: duplicate message within 15 minutes.",
                            "156" => "Vodacom SMS rejected: no route for this cellphone number.",
                            "162" => "Vodacom SMS rejected: number is on the Do Not Contact list.",
                            _ => "Vodacom did not accept the SMS. Check the provider sent logs."
                        });
                    if (Read(result, "Action") != "enqueued" || NormalizeResponseNumber(Read(result, "Number")) != number
                        || string.IsNullOrWhiteSpace(Read(result, "Key"))) throw InvalidResponse();
                }
            }
            catch (JsonException) { throw InvalidResponse(); }
        }
    }

    private static string NormalizeResponseNumber(string? value)
    {
        var number = Regex.Replace(value?.Trim() ?? "", "[ ()-]", "");
        if (number.StartsWith("+27", StringComparison.Ordinal)) number = number[1..];
        if (number.StartsWith('0')) number = "27" + number[1..];
        return number;
    }

    private static string? Read(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) ? item.ToString() : null;

    private static InvalidOperationException InvalidResponse() => new(
        "Vodacom returned an unrecognised SMS response. Delivery status is unknown; check the provider sent logs before retrying.");
}
