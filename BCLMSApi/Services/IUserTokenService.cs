using BCLMSApi.Models;

namespace BCLMSApi.Services;

public interface IUserTokenService
{
    string CreateToken(SystemUser user);

    UserTokenPayload? GetValidTokenPayload(string? authorizationHeader);

    bool IsInternalUserToken(string? authorizationHeader);

    bool IsSuperUserToken(string? authorizationHeader);
}
