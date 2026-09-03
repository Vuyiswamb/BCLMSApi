using BCLMSApi.Models;

namespace BCLMSApi.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);

    Task<PasswordResetRequestResponse> RequestPasswordResetAsync(PasswordResetRequest request, string? ipAddress);

    Task<bool> ValidateResetTokenAsync(string token);

    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);

    Task<List<GroupResponse>> GetGroupsAsync(bool includeCustomer);

    Task<List<ManagedUserResponse>> GetUsersAsync();

    Task<ManagedUserResponse> CreateOfficialUserAsync(CreateOfficialUserRequest request);

    Task<ManagedUserResponse> UpdateOfficialUserAsync(int userId, UpdateOfficialUserRequest request);

    Task<PasswordResetByAdminResponse> ResetUserPasswordByAdminAsync(int userId);

    Task<PasswordResetByAdminResponse> SetUserPasswordByAdminAsync(int userId, SetUserPasswordByAdminRequest request);

    Task<ManagedUserResponse> RegisterCustomerAsync(RegisterCustomerRequest request);
}
