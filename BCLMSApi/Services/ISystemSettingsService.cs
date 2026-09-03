using BCLMSApi.Models;

namespace BCLMSApi.Services;

public interface ISystemSettingsService
{
    Task<EmailSettingsResponse> GetEmailSettingsAsync();
    Task<EmailSettingsResponse> UpdateEmailSettingsAsync(EmailSettingsUpdateRequest request);
    Task<string?> GetEmailPasswordAsync();
}
