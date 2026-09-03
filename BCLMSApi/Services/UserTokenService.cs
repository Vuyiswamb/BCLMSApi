using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCLMSApi.Models;

namespace BCLMSApi.Services;

public class UserTokenService(IConfiguration configuration) : IUserTokenService
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
        var token = authorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorizationHeader["Bearer ".Length..].Trim()
            : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 2 || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(parts[0])),
            Encoding.UTF8.GetBytes(parts[1])))
        {
            return null;
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        var payload = JsonSerializer.Deserialize<UserTokenPayload>(payloadJson);
        return payload is not null && payload.ExpiresAtUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            ? payload
            : null;
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
