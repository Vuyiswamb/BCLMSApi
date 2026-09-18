using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCLMSApi.Models;

namespace BCLMSApi.Services;

public class UserTokenService(IConfiguration configuration, ILogger<UserTokenService>? logger = null) : IUserTokenService
{
    private const int TokenHoursToLive = 8;

    public string CreateToken(SystemUser user)
    {
        var payload = new UserTokenPayload
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Groups = user.Groups,
            HasAllRegions = user.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase) || user.HasAllRegions,
            Regions = user.Regions,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(TokenHoursToLive).ToUnixTimeSeconds()
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadText = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signature = Sign(payloadText);

        return $"{payloadText}.{signature}";
    }

    public bool IsSuperUserToken(string? authorizationHeader)
    {
        var payload = GetValidTokenPayload(authorizationHeader);
        return payload is not null
            && payload.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase);
    }

    public bool IsInternalUserToken(string? authorizationHeader)
    {
        var payload = GetValidTokenPayload(authorizationHeader);
        return payload is not null
            && !payload.Groups.Contains("Customer", StringComparer.OrdinalIgnoreCase);
    }

    public UserTokenPayload? GetValidTokenPayload(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return Reject("missing_authorization_header");
        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Reject("invalid_authorization_scheme");
        var token = authorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorizationHeader["Bearer ".Length..].Trim()
            : null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16384)
        {
            return Reject(string.IsNullOrWhiteSpace(token) ? "empty_bearer_token" : "token_too_long");
        }

        var parts = token.Split('.');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            return Reject("invalid_token_parts");
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(parts[0])),
            Encoding.UTF8.GetBytes(parts[1])))
        {
            return Reject("signature_mismatch");
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payload = JsonSerializer.Deserialize<UserTokenPayload>(payloadJson);
            if (payload is null || payload.UserId <= 0 || string.IsNullOrWhiteSpace(payload.Username)
                || payload.Groups is null || payload.Regions is null)
                return Reject("missing_required_payload_fields");
            if (payload.ExpiresAtUtc <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return Reject("token_expired");
            return payload;
        }
        catch (Exception error) when (error is FormatException or JsonException)
        {
            return Reject(error is FormatException ? "invalid_payload_encoding" : "invalid_payload_json");
        }
    }

    private UserTokenPayload? Reject(string reason)
    {
        // Never log the Authorization header, token, decoded identity or signing secret.
        logger?.LogWarning("Bearer token rejected: {Reason}; trace {TraceId}; server UTC {ServerUtc}",
            reason, System.Diagnostics.Activity.Current?.TraceId.ToString(), DateTimeOffset.UtcNow);
        return null;
    }

    private string Sign(string payload)
    {
        var secret = configuration["PasswordSecurity:Pepper"]
            ?? throw new InvalidOperationException("PasswordSecurity:Pepper is missing from configuration.");
        using var hmac = new HMACSHA256(Convert.FromBase64String(secret));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var paddedValue = value.Replace('-', '+').Replace('_', '/');
        paddedValue = paddedValue.PadRight(paddedValue.Length + (4 - paddedValue.Length % 4) % 4, '=');
        return Convert.FromBase64String(paddedValue);
    }
}
