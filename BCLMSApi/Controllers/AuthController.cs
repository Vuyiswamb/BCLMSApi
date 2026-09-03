using System.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics(string? username = null)
    {
        if (!IsDiagnosticsRequestAllowed())
        {
            return NotFound();
        }

        var pepper = configuration["PasswordSecurity:Pepper"];
        var diagnostics = new Dictionary<string, object?>
        {
            ["environment"] = environment.EnvironmentName,
            ["hasConnectionString"] = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("BclmsConnection")),
            ["hasPasswordPepper"] = !string.IsNullOrWhiteSpace(pepper),
            ["passwordPepperIsBase64"] = IsBase64(pepper),
            ["authGetUserForLoginProcedureExists"] = false,
            ["authUpdateLastLoginProcedureExists"] = false,
            ["databaseReachable"] = false
        };

        try
        {
            await using var connection = new SqlConnection(configuration.GetConnectionString("BclmsConnection"));
            await connection.OpenAsync();
            diagnostics["databaseReachable"] = true;

            await using (var command = new SqlCommand("""
                SELECT
                    CAST(CASE WHEN OBJECT_ID('dbo.usp_Auth_GetUserForLogin', 'P') IS NULL THEN 0 ELSE 1 END AS BIT) AS GetUserForLoginExists,
                    CAST(CASE WHEN OBJECT_ID('dbo.usp_Auth_UpdateLastLogin', 'P') IS NULL THEN 0 ELSE 1 END AS BIT) AS UpdateLastLoginExists;
                """, connection))
            {
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    diagnostics["authGetUserForLoginProcedureExists"] = reader.GetBoolean(reader.GetOrdinal("GetUserForLoginExists"));
                    diagnostics["authUpdateLastLoginProcedureExists"] = reader.GetBoolean(reader.GetOrdinal("UpdateLastLoginExists"));
                }
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                await using var userCommand = new SqlCommand("""
                    SELECT TOP (1)
                        UserId,
                        IsActive,
                        PasswordHash,
                        PasswordSalt,
                        PasswordIterations,
                        (
                            SELECT COUNT(*)
                            FROM dbo.UserGroups userGroups
                            WHERE userGroups.UserId = users.UserId
                        ) AS GroupCount
                    FROM dbo.Users users
                    WHERE users.Username = @Username
                       OR users.EmailAddress = @Username;
                    """, connection);
                userCommand.Parameters.AddWithValue("@Username", username.Trim());

                await using var userReader = await userCommand.ExecuteReaderAsync();
                if (await userReader.ReadAsync())
                {
                    var passwordHash = userReader.IsDBNull(userReader.GetOrdinal("PasswordHash")) ? null : userReader.GetString(userReader.GetOrdinal("PasswordHash"));
                    var passwordSalt = userReader.IsDBNull(userReader.GetOrdinal("PasswordSalt")) ? null : userReader.GetString(userReader.GetOrdinal("PasswordSalt"));

                    diagnostics["userFound"] = true;
                    diagnostics["userId"] = userReader.GetInt32(userReader.GetOrdinal("UserId"));
                    diagnostics["isActive"] = userReader.GetBoolean(userReader.GetOrdinal("IsActive"));
                    diagnostics["hasPasswordHash"] = !string.IsNullOrWhiteSpace(passwordHash);
                    diagnostics["passwordHashIsBase64"] = IsBase64(passwordHash);
                    diagnostics["hasPasswordSalt"] = !string.IsNullOrWhiteSpace(passwordSalt);
                    diagnostics["passwordSaltIsBase64"] = IsBase64(passwordSalt);
                    diagnostics["passwordIterations"] = userReader.GetInt32(userReader.GetOrdinal("PasswordIterations"));
                    diagnostics["groupCount"] = userReader.GetInt32(userReader.GetOrdinal("GroupCount"));
                }
                else
                {
                    diagnostics["userFound"] = false;
                }
            }

            return Ok(diagnostics);
        }
        catch (Exception error)
        {
            diagnostics["databaseErrorType"] = error.GetType().Name;
            diagnostics["databaseError"] = error.Message;
            diagnostics["innerDatabaseError"] = error.InnerException?.Message;
            return StatusCode(StatusCodes.Status500InternalServerError, diagnostics);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var username = request.Username?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        try
        {
            var response = await authService.LoginAsync(new LoginRequest
            {
                Username = username,
                Password = request.Password
            });

            return response is null
                ? Unauthorized(new { message = "Invalid username or password." })
                : Ok(response);
        }
        catch (SqlException error)
        {
            logger.LogError(error, "Database error while signing in user {Username}.", username);
            return LoginFailure(error);
        }
        catch (InvalidOperationException error)
        {
            logger.LogError(error, "Configuration or login processing error while signing in user {Username}.", username);
            return LoginFailure(error);
        }
        catch (FormatException error)
        {
            logger.LogError(error, "Invalid password security data while signing in user {Username}.", username);
            return LoginFailure(error);
        }
        catch (Exception error)
        {
           logger.LogError(error, "Unexpected error while signing in user {Username}.", username);
           return LoginFailure(error);
        }
    }

    [HttpPost("request-password-reset")]
    public async Task<ActionResult<PasswordResetRequestResponse>> RequestPasswordReset(PasswordResetRequest request)
    {
        try
        {  
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
        {
            return BadRequest(new { message = "Username or email address is required." });
        }

        var response = await authService.RequestPasswordResetAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(response);

        }
        catch (Exception error)
        {
            logger.LogError(error, "Unexpected error while requesting password reset.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to request a password reset right now." });
        }
    }

    [HttpGet("validate-reset-token")]
    public async Task<ActionResult<ValidateResetTokenResponse>> ValidateResetToken(string token)
    {
        try
        {  
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { message = "Reset token is required." });
        }

        return Ok(new ValidateResetTokenResponse { IsValid = await authService.ValidateResetTokenAsync(token) });

        }
        catch (Exception error)
        {
            logger.LogError(error, "Unexpected error while validating password reset token.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to validate the reset token right now." });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        try
        {

            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Reset token and new password are required." });
            }

            if (request.NewPassword.Length < 12)
            {
                return BadRequest(new { message = "Password must be at least 12 characters long." });
            }

            if (!await authService.ResetPasswordAsync(request))
            {
                return BadRequest(new { message = "The reset link is invalid or has expired." });
            }

            return Ok(new { message = "Password reset successfully." });
        }
        catch (Exception error)
        {
            logger.LogError(error, "Unexpected error while resetting password.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to reset the password right now." });
        }
    }

    private ObjectResult LoginFailure(Exception error)
    {
        var exposeLoginErrors = configuration.GetValue<bool>("Diagnostics:ExposeLoginErrors") || IsDiagnosticsRequestAllowed();
        if (!exposeLoginErrors)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to sign in right now. Please contact support." });
        }

        return StatusCode(StatusCodes.Status500InternalServerError, new
        {
            message = "Unable to sign in right now. Please contact support.",
            environment = environment.EnvironmentName,
            errorType = error.GetType().Name,
            detail = error.Message,
            innerDetail = error.InnerException?.Message
        });
    }

    private bool IsDiagnosticsRequestAllowed()
    {
        var configuredKey = configuration["Diagnostics:AuthKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return false;
        }

        var providedKey = Request.Headers["X-Diagnostics-Key"].ToString();
        return string.Equals(configuredKey, providedKey, StringComparison.Ordinal);
    }

    private static bool IsBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
