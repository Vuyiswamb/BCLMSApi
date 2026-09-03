using System.Security.Cryptography;
using System.Net;
using BCLMSApi.Data;
using BCLMSApi.Models;

namespace BCLMSApi.Services;

public class AuthService(IAuthRepository authRepository, IConfiguration configuration, IEmailService emailService, IUserTokenService userTokenService) : IAuthService
{
    private const int PasswordHashBytes = 32;
    private const int PasswordSaltBytes = 16;
    private const int PasswordIterations = 210000;
    private const int ResetTokenHoursToLive = 2;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await authRepository.GetUserForLoginAsync(request.Username ?? string.Empty);
        if (user is null
            || !user.IsActive
            || !VerifyPassword(request.Password ?? string.Empty, user.PasswordSalt, user.PasswordHash, user.PasswordIterations))
        {
            return null;
        }

        await authRepository.UpdateLastLoginAsync(user.UserId);

        return new LoginResponse
        {
            Username = user.Username,
            DisplayName = user.DisplayName,
            Groups = user.Groups,
            IsSuperUser = user.Groups.Contains("Super User"),
            HasAllRegions = user.Groups.Contains("Super User") || user.HasAllRegions,
            Regions = user.Regions,
            Token = userTokenService.CreateToken(user)
        };
    }

    public async Task<PasswordResetRequestResponse> RequestPasswordResetAsync(PasswordResetRequest request, string? ipAddress)
    {
        var response = new PasswordResetRequestResponse
        {
            Message = "If the account exists, a password reset link will be sent."
        };

        var user = await authRepository.GetUserByUsernameOrEmailAsync(request.UsernameOrEmail ?? string.Empty);
        if (user is null)
        {
            return response;
        }

        var token = CreateUrlSafeToken(32);
        var tokenHash = HashResetToken(token);
        await authRepository.SavePasswordResetTokenAsync(user.UserId, tokenHash, ResetTokenHoursToLive, ipAddress);

        response.ResetLink = $"{GetPortalBaseUrl()}/account?resetToken={Uri.EscapeDataString(token)}";
        var emailAddress = string.IsNullOrWhiteSpace(user.EmailAddress) ? user.Username : user.EmailAddress;
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            throw new InvalidOperationException("The user does not have an email address.");
        }

        var subject = "BCLMS password reset";
        var body = BuildPasswordResetEmailBody(user.DisplayName, response.ResetLink, ResetTokenHoursToLive);
        await emailService.SendEmailOffice365Async(emailAddress, subject, body);

        return response;
    }

    public async Task<bool> ValidateResetTokenAsync(string token)
    {
        return await authRepository.GetValidResetTokenAsync(HashResetToken(token)) is not null;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var tokenHash = HashResetToken(request.Token ?? string.Empty);
        var resetToken = await authRepository.GetValidResetTokenAsync(tokenHash);
        if (resetToken is null)
        {
            return false;
        }

        var passwordResult = HashPassword(request.NewPassword ?? string.Empty);
        await authRepository.UpdatePasswordAsync(resetToken.UserId, passwordResult.Hash, passwordResult.Salt, passwordResult.Iterations, tokenHash);
        return true;
    }

    public Task<List<GroupResponse>> GetGroupsAsync(bool includeCustomer)
    {
        return authRepository.GetGroupsAsync(includeCustomer);
    }

    public Task<List<ManagedUserResponse>> GetUsersAsync()
    {
        return authRepository.GetUsersAsync();
    }

    public async Task<ManagedUserResponse> CreateOfficialUserAsync(CreateOfficialUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.EmailAddress)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Display name, email address, and password are required.");
        }

        if (request.Password.Length < 12)
        {
            throw new ArgumentException("Password must be at least 12 characters long.");
        }

        var activeGroups = await authRepository.GetGroupsAsync(includeCustomer: false);
        var allowedGroupIds = activeGroups.Select(group => group.GroupId).ToHashSet();
        var selectedGroupIds = request.GroupIds.Where(allowedGroupIds.Contains).Distinct().ToList();
        if (selectedGroupIds.Count == 0)
        {
            throw new ArgumentException("Select at least one internal permission group.");
        }

        var passwordResult = HashPassword(request.Password);
        var emailAddress = request.EmailAddress.Trim();
        return await authRepository.CreateUserAsync(
            emailAddress,
            request.DisplayName.Trim(),
            emailAddress,
            passwordResult.Hash,
            passwordResult.Salt,
            passwordResult.Iterations,
            selectedGroupIds,
            request.HasAllRegions,
            request.Regions);
    }

    public async Task<ManagedUserResponse> UpdateOfficialUserAsync(int userId, UpdateOfficialUserRequest request)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("Select a valid user.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.EmailAddress))
        {
            throw new ArgumentException("Display name and email address are required.");
        }

        var activeGroups = await authRepository.GetGroupsAsync(includeCustomer: false);
        var allowedGroupIds = activeGroups.Select(group => group.GroupId).ToHashSet();
        var selectedGroupIds = request.GroupIds.Where(allowedGroupIds.Contains).Distinct().ToList();
        if (selectedGroupIds.Count == 0)
        {
            throw new ArgumentException("Select at least one internal permission group.");
        }

        var emailAddress = request.EmailAddress.Trim();
        return await authRepository.UpdateUserAsync(
            userId,
            emailAddress,
            request.DisplayName.Trim(),
            emailAddress,
            request.IsActive,
            selectedGroupIds,
            request.HasAllRegions,
            request.Regions);
    }

    public async Task<PasswordResetByAdminResponse> ResetUserPasswordByAdminAsync(int userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("Select a valid user.");
        }

        var temporaryPassword = CreateTemporaryPassword();
        var passwordResult = HashPassword(temporaryPassword);
        var user = await authRepository.ResetUserPasswordAsync(
            userId,
            passwordResult.Hash,
            passwordResult.Salt,
            passwordResult.Iterations);

        var emailAddress = string.IsNullOrWhiteSpace(user.EmailAddress) ? user.Username : user.EmailAddress;
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            throw new InvalidOperationException("The user does not have an email address.");
        }

        await emailService.SendEmailOffice365Async(
            emailAddress,
            "BCLMS temporary password",
            BuildAdminPasswordResetEmailBody(user.DisplayName, temporaryPassword));

        return new PasswordResetByAdminResponse
        {
            Message = $"Temporary password sent to {emailAddress}."
        };
    }

    public async Task<PasswordResetByAdminResponse> SetUserPasswordByAdminAsync(int userId, SetUserPasswordByAdminRequest request)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("Select a valid user.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
        {
            throw new ArgumentException("Password must be at least 12 characters long.");
        }

        var passwordResult = HashPassword(request.NewPassword);
        var user = await authRepository.ResetUserPasswordAsync(
            userId,
            passwordResult.Hash,
            passwordResult.Salt,
            passwordResult.Iterations);

        return new PasswordResetByAdminResponse
        {
            Message = $"Password updated for {user.DisplayName}."
        };
    }

    public async Task<ManagedUserResponse> RegisterCustomerAsync(RegisterCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Full name, email address, and password are required.");
        }

        if (request.Password.Length < 12)
        {
            throw new ArgumentException("Password must be at least 12 characters long.");
        }

        var customerGroup = (await authRepository.GetGroupsAsync(includeCustomer: true))
            .FirstOrDefault(group => group.GroupName.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Customer group is not configured.");
        var passwordResult = HashPassword(request.Password);
        var emailAddress = request.Email.Trim();

        var user = await authRepository.CreateUserAsync(
            emailAddress,
            request.FullName.Trim(),
            emailAddress,
            passwordResult.Hash,
            passwordResult.Salt,
            passwordResult.Iterations,
            [customerGroup.GroupId],
            true,
            []);

        try
        {
            await emailService.SendEmailOffice365Async(
                emailAddress,
                "Welcome to BCLMS",
                BuildWelcomeEmailBody(user.DisplayName, GetPortalBaseUrl()));
        }
        catch
        {
            // Registration must not fail after the account has already been created.
        }

        return user;
    }

    private bool VerifyPassword(string password, string salt, string expectedHash, int iterations)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var pepperBytes = GetConfiguredPepperBytes();
        var saltBytes = ReadBase64Bytes(salt, "PasswordSalt");
        var combinedPassword = passwordBytes.Concat(pepperBytes).ToArray();

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            combinedPassword,
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256,
            PasswordHashBytes);

        var expectedBytes = ReadBase64Bytes(expectedHash, "PasswordHash");
        return CryptographicOperations.FixedTimeEquals(hashBytes, expectedBytes);
    }

    private PasswordHashResult HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(PasswordSaltBytes);
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var pepperBytes = GetConfiguredPepperBytes();
        var combinedPassword = passwordBytes.Concat(pepperBytes).ToArray();

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            combinedPassword,
            saltBytes,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            PasswordHashBytes);

        return new PasswordHashResult
        {
            Hash = Convert.ToBase64String(hashBytes),
            Salt = Convert.ToBase64String(saltBytes),
            Iterations = PasswordIterations
        };
    }

    private byte[] GetConfiguredPepperBytes()
    {
        var pepper = configuration["PasswordSecurity:Pepper"];
        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException("PasswordSecurity:Pepper is missing from configuration.");
        }

        return ReadBase64Bytes(pepper, "PasswordSecurity:Pepper");
    }

    private static byte[] ReadBase64Bytes(string value, string fieldName)
    {
        var trimmedValue = value.Trim();
        try
        {
            return Convert.FromBase64String(trimmedValue);
        }
        catch (FormatException error)
        {
            throw new InvalidOperationException($"{fieldName} must be a valid Base64 string.", error);
        }
    }

    private static string HashResetToken(string token)
    {
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static string CreateUrlSafeToken(int byteCount)
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string CreateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        const string allCharacters = uppercase + lowercase + digits + symbols;

        var requiredCharacters = new[]
        {
            uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)],
            lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        var remainingCharacters = Enumerable.Range(0, 8)
            .Select(_ => allCharacters[RandomNumberGenerator.GetInt32(allCharacters.Length)]);

        return new string(requiredCharacters.Concat(remainingCharacters)
            .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
            .ToArray());
    }

    private string GetPortalBaseUrl()
    {
        return configuration["Portal:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:4201";
    }

    private static string BuildPasswordResetEmailBody(string displayName, string resetLink, int hoursToLive)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>BCLMS Password Reset</title>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; background:#f5f7f8; margin:0; padding:24px;'>
    <div style='max-width:640px; margin:0 auto; background:#ffffff; border-radius:8px; overflow:hidden; border:1px solid #dfe6ec;'>
        <div style='background:#0b3d2f; color:#ffffff; padding:28px 32px;'>
            <h1 style='margin:0; font-size:24px;'>Business Compliance & Licensing</h1>
            <p style='margin:8px 0 0;'>Password reset request</p>
        </div>
        <div style='padding:32px; color:#102033; line-height:1.6;'>
            <p>Dear {WebUtility.HtmlEncode(displayName)},</p>
            <p>We received a request to reset your BCLMS password. Use the secure link below to create a new password.</p>
            <p style='margin:30px 0;'>
                <a href='{WebUtility.HtmlEncode(resetLink)}' style='display:inline-block; background:#16821f; color:#ffffff; padding:14px 24px; border-radius:7px; font-weight:bold; text-decoration:none;'>Reset password</a>
            </p>
            <p>This link expires in {hoursToLive} hour(s). If you did not request this reset, you can ignore this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildAdminPasswordResetEmailBody(string displayName, string temporaryPassword)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>BCLMS Temporary Password</title>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; background:#f5f7f8; margin:0; padding:24px;'>
    <div style='max-width:640px; margin:0 auto; background:#ffffff; border-radius:8px; overflow:hidden; border:1px solid #dfe6ec;'>
        <div style='background:#0b3d2f; color:#ffffff; padding:28px 32px;'>
            <h1 style='margin:0; font-size:24px;'>Business Compliance & Licensing</h1>
            <p style='margin:8px 0 0;'>Temporary password issued</p>
        </div>
        <div style='padding:32px; color:#102033; line-height:1.6;'>
            <p>Dear {WebUtility.HtmlEncode(displayName)},</p>
            <p>A BCLMS administrator has reset your account password. Use the temporary password below to sign in, then change it to a secure password known only to you.</p>
            <div style='background:#f3f8f4; border:1px solid #b9dfc0; border-radius:8px; padding:18px 20px; margin:24px 0;'>
                <p style='margin:0 0 8px; color:#52657a; font-weight:bold;'>Temporary password</p>
                <p style='margin:0; color:#102033; font-size:22px; font-weight:bold; letter-spacing:.04em;'>{WebUtility.HtmlEncode(temporaryPassword)}</p>
            </div>
            <p>Please keep this password private. If you did not expect this reset, contact the BCLMS support team immediately.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string BuildWelcomeEmailBody(string displayName, string portalBaseUrl)
    {
        var encodedDisplayName = WebUtility.HtmlEncode(displayName);
        var encodedPortalBaseUrl = WebUtility.HtmlEncode(portalBaseUrl);
        var logoUrl = $"{portalBaseUrl}/assets/cotlogo.png";
        var heroImageUrl = $"{portalBaseUrl}/assets/background_image_city.png";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Welcome to BCLMS</title>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; background:#edf2f5; margin:0; padding:24px;'>
    <div style='display:none; max-height:0; overflow:hidden; opacity:0;'>Your BCLMS account is ready. You can now submit and track business licensing applications online.</div>
    <div style='max-width:680px; margin:0 auto; background:#ffffff; border-radius:8px; overflow:hidden; border:1px solid #d8e2e8;'>
        <div style='background:#ffffff; padding:22px 30px; border-bottom:4px solid #16821f;'>
            <img src='{WebUtility.HtmlEncode(logoUrl)}' alt='City of Tshwane' width='148' style='display:block; max-width:148px; height:auto;' />
        </div>
        <div>
            <img src='{WebUtility.HtmlEncode(heroImageUrl)}' alt='City of Tshwane skyline' width='680' style='display:block; width:100%; max-width:680px; height:auto;' />
        </div>
        <div style='padding:34px 34px 28px; color:#102033; line-height:1.65;'>
            <p style='margin:0 0 12px; color:#16821f; font-weight:bold; letter-spacing:.04em; text-transform:uppercase; font-size:12px;'>Business Compliance & Licensing Management System</p>
            <h1 style='margin:0 0 18px; font-size:28px; line-height:1.25; color:#0f2f25;'>Welcome to BCLMS</h1>
            <p style='margin:0 0 18px;'>Dear {encodedDisplayName},</p>
            <p style='margin:0 0 18px;'>Your customer account has been created successfully. You can now use the City of Tshwane BCLMS portal to submit business licence applications, manage your details, and track application progress online.</p>
            <div style='background:#f3f8f4; border-left:4px solid #16821f; padding:18px 20px; margin:26px 0;'>
                <p style='margin:0 0 10px; font-weight:bold; color:#0f2f25;'>With your account, you can:</p>
                <p style='margin:0;'>Submit new applications, upload supporting documents, monitor application status, and receive updates as your application moves through the review process.</p>
            </div>
            <p style='margin:30px 0;'>
                <a href='{encodedPortalBaseUrl}/account' style='display:inline-block; background:#16821f; color:#ffffff; padding:14px 24px; border-radius:6px; font-weight:bold; text-decoration:none;'>Log in to BCLMS</a>
            </p>
            <p style='margin:0 0 18px;'>Please keep your login details secure and ensure the information you provide on applications is accurate and complete.</p>
            <p style='margin:0;'>Regards,<br />City of Tshwane<br />Business Compliance & Licensing</p>
        </div>
        <div style='background:#102033; color:#dce7ed; padding:20px 34px; font-size:12px; line-height:1.5;'>
            <p style='margin:0 0 8px;'>This is an automated message from BCLMS. Please do not reply to this email.</p>
            <p style='margin:0;'>City of Tshwane Metropolitan Municipality</p>
        </div>
    </div>
</body>
</html>";
    }
}
