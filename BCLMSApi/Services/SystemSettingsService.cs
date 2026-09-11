using System.Security.Cryptography;
using System.Text;
using BCLMSApi.Data;
using BCLMSApi.Models;

namespace BCLMSApi.Services;

public class SystemSettingsService(Datalayer datalayer, IConfiguration configuration) : ISystemSettingsService
{
    private const string EmailHostKey = "Email.Office365.Host";
    private const string EmailPortKey = "Email.Office365.Port";
    private const string EmailFromAddressKey = "Email.Office365.FromAddress";
    private const string EmailUsernameKey = "Email.Office365.Username";
    private const string EmailPasswordKey = "Email.Office365.Password";
    private const string HanisBaseUrlKey = "Hanis.BaseUrl";
    private const string HanisUsernameKey = "Hanis.Username";
    private const string HanisDepartmentKey = "Hanis.Department";
    private const string HanisAllowInsecureQaKey = "Hanis.AllowInsecureQa";
    private const string HanisApiKeyKey = "Hanis.ApiKey";

    public async Task<EmailSettingsResponse> GetEmailSettingsAsync()
    {
        var host = await GetSettingAsync(EmailHostKey) ?? configuration["Email:Office365:Host"] ?? "smtp.office365.com";
        var port = await GetSettingAsync(EmailPortKey) ?? configuration["Email:Office365:Port"] ?? "587";
        var fromAddress = await GetSettingAsync(EmailFromAddressKey) ?? configuration["Email:Office365:FromAddress"] ?? "BCR@TSHWANE.GOV.ZA";
        var username = await GetSettingAsync(EmailUsernameKey) ?? configuration["Email:Office365:Username"] ?? fromAddress;
        var password = await GetEmailPasswordAsync();
        return new EmailSettingsResponse
        {
            Host = host,
            Port = port,
            FromAddress = fromAddress,
            Username = username,
            HasPassword = !string.IsNullOrWhiteSpace(password),
            PasswordMask = string.IsNullOrWhiteSpace(password) ? "Not configured" : "********",
            Password = password ?? string.Empty
        };
    }

    public async Task<EmailSettingsResponse> UpdateEmailSettingsAsync(EmailSettingsUpdateRequest request)
    {
        await SaveSettingAsync(EmailHostKey, request.Host.Trim(), isSecret: false);
        await SaveSettingAsync(EmailPortKey, request.Port.Trim(), isSecret: false);
        await SaveSettingAsync(EmailFromAddressKey, request.FromAddress.Trim(), isSecret: false);
        await SaveSettingAsync(EmailUsernameKey, request.Username.Trim(), isSecret: false);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            await SaveSettingAsync(EmailPasswordKey, Protect(request.Password.Trim()), isSecret: true);
        }

        return await GetEmailSettingsAsync();
    }

    public async Task<string?> GetEmailPasswordAsync()
    {
        var protectedValue = await GetSettingAsync(EmailPasswordKey);
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        return Unprotect(protectedValue);
    }

    public async Task<HanisSettingsResponse> GetHanisSettingsAsync()
    {
        var apiKey = await GetHanisApiKeyAsync();
        return new HanisSettingsResponse
        {
            BaseUrl = await GetSettingAsync(HanisBaseUrlKey) ?? configuration["Hanis:BaseUrl"] ?? string.Empty,
            Username = await GetSettingAsync(HanisUsernameKey) ?? configuration["Hanis:Username"] ?? string.Empty,
            Department = await GetSettingAsync(HanisDepartmentKey) ?? configuration["Hanis:Department"] ?? "Economic Development",
            AllowInsecureQa = bool.TryParse(await GetSettingAsync(HanisAllowInsecureQaKey) ?? configuration["Hanis:AllowInsecureQa"], out var allowInsecureQa) && allowInsecureQa,
            HasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            ApiKeyMask = string.IsNullOrWhiteSpace(apiKey) ? "Not configured" : "********",
            ApiKey = apiKey ?? string.Empty
        };
    }

    public async Task<HanisSettingsResponse> UpdateHanisSettingsAsync(HanisSettingsUpdateRequest request)
    {
        if (!Uri.TryCreate(request.BaseUrl?.Trim(), UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Enter a valid Home Affairs base URL without credentials, query parameters, or fragments.");
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException("Enter the Home Affairs API username.");

        await SaveSettingAsync(HanisBaseUrlKey, uri.GetLeftPart(UriPartial.Path).TrimEnd('/'), false);
        await SaveSettingAsync(HanisUsernameKey, request.Username.Trim(), false);
        await SaveSettingAsync(HanisDepartmentKey, request.Department.Trim(), false);
        await SaveSettingAsync(HanisAllowInsecureQaKey, request.AllowInsecureQa.ToString(), false);
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) await SaveSettingAsync(HanisApiKeyKey, Protect(request.ApiKey.Trim()), true);
        return await GetHanisSettingsAsync();
    }

    public async Task<string?> GetHanisApiKeyAsync()
    {
        var protectedValue = await GetSettingAsync(HanisApiKeyKey);
        return string.IsNullOrWhiteSpace(protectedValue) ? configuration["Hanis:ApiKey"] : Unprotect(protectedValue);
    }

    private async Task<string?> GetSettingAsync(string settingKey)
    {
        await using var command = datalayer.CreateTextCommand("""
            SELECT SettingValue
            FROM dbo.SystemSettings
            WHERE SettingKey = @SettingKey;
            """);
        command.Parameters.AddWithValue("@SettingKey", settingKey);
        await command.Connection!.OpenAsync();
        var value = await command.ExecuteScalarAsync();
        return value == DBNull.Value ? null : value?.ToString();
    }

    private async Task SaveSettingAsync(string settingKey, string settingValue, bool isSecret)
    {
        await using var command = datalayer.CreateTextCommand("""
            MERGE dbo.SystemSettings AS target
            USING (SELECT @SettingKey AS SettingKey) AS source
            ON target.SettingKey = source.SettingKey
            WHEN MATCHED THEN
                UPDATE SET SettingValue = @SettingValue, IsSecret = @IsSecret, ModifiedDate = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (SettingKey, SettingValue, IsSecret)
                VALUES (@SettingKey, @SettingValue, @IsSecret);
            """);
        command.Parameters.AddWithValue("@SettingKey", settingKey);
        command.Parameters.AddWithValue("@SettingValue", settingValue);
        command.Parameters.AddWithValue("@IsSecret", isSecret);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    private string Protect(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(GetSecretKey(), 16);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipherBytes).ToArray());
    }

    private string Unprotect(string protectedValue)
    {
        var payload = Convert.FromBase64String(protectedValue);
        var nonce = payload[..12];
        var tag = payload[12..28];
        var cipherBytes = payload[28..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(GetSecretKey(), 16);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private byte[] GetSecretKey()
    {
        var secret = configuration["PasswordSecurity:Pepper"]
            ?? throw new InvalidOperationException("PasswordSecurity:Pepper is missing from configuration.");
        return SHA256.HashData(Convert.FromBase64String(secret.Trim()));
    }
}
