using BCLMSApi.Models;

namespace BCLMSApi.Data;

public interface IAuthRepository
{
    Task<SystemUser?> GetUserForLoginAsync(string username);

    Task<ResetUser?> GetUserByUsernameOrEmailAsync(string usernameOrEmail);

    Task UpdateLastLoginAsync(int userId);

    Task SavePasswordResetTokenAsync(int userId, string tokenHash, int hoursToLive, string? ipAddress);

    Task<PasswordResetToken?> GetValidResetTokenAsync(string tokenHash);

    Task UpdatePasswordAsync(int userId, string passwordHash, string passwordSalt, int iterations, string tokenHash);

    Task<List<GroupResponse>> GetGroupsAsync(bool includeCustomer);

    Task<List<ManagedUserResponse>> GetUsersAsync();

    Task<ManagedUserResponse> CreateUserAsync(string username, string displayName, string emailAddress, string passwordHash, string passwordSalt, int passwordIterations, List<int> groupIds, bool hasAllRegions, List<string> regions);

    Task<ManagedUserResponse> UpdateUserAsync(int userId, string username, string displayName, string emailAddress, bool isActive, List<int> groupIds, bool hasAllRegions, List<string> regions);

    Task<ManagedUserResponse> ResetUserPasswordAsync(int userId, string passwordHash, string passwordSalt, int passwordIterations);
}
