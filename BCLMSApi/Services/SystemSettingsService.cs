using System.Security.Cryptography;
using System.Text;
using BCLMSApi.Data;
using BCLMSApi.Models;

namespace BCLMSApi.Services;

public class SystemSettingsService(Datalayer datalayer, IConfiguration configuration) : ISystemSettingsService
{
    private const string EmailPasswordKey = "Email.Office365.Password";

    public async Task<EmailSettingsResponse> GetEmailSettingsAsync()
    {
        var password = await GetEmailPasswordAsync();
        return new EmailSettingsResponse
        {
            Host = configuration["Email:Office365:Host"] ?? "smtp.office365.com",
            Port = configuration["Email:Office365:Port"] ?? "587",
            FromAddress = configuration["Email:Office365:FromAddress"] ?? "Ithuba@TSHWANE.GOV.ZA",
            Username = configuration["Email:Office365:Username"] ?? "Ithuba@TSHWANE.GOV.ZA",
            HasPassword = !string.IsNullOrWhiteSpace(password),
            PasswordMask = string.IsNullOrWhiteSpace(password) ? "Not configured" : "********",
            Password = password ?? string.Empty
        };
    }

    public async Task<EmailSettingsResponse> UpdateEmailSettingsAsync(EmailSettingsUpdateRequest request)
    {
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
