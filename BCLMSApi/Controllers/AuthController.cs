using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, AuthDiagnosticsRepository diagnosticsRepository, ILogger<AuthController> logger, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
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
            diagnostics["databaseReachable"] = true;
            var data = await diagnosticsRepository.GetAsync(username);
            diagnostics["authGetUserForLoginProcedureExists"] = data.GetUserForLoginProcedureExists;
            diagnostics["authUpdateLastLoginProcedureExists"] = data.UpdateLastLoginProcedureExists;
            if (data.User is not null)
            {
                var user=data.User; diagnostics["userFound"]=true;diagnostics["userId"]=user.UserId;diagnostics["isActive"]=user.IsActive;diagnostics["hasPasswordHash"]=!string.IsNullOrWhiteSpace(user.PasswordHash);diagnostics["passwordHashIsBase64"]=IsBase64(user.PasswordHash);diagnostics["hasPasswordSalt"]=!string.IsNullOrWhiteSpace(user.PasswordSalt);diagnostics["passwordSaltIsBase64"]=IsBase64(user.PasswordSalt);diagnostics["passwordIterations"]=user.PasswordIterations;diagnostics["groupCount"]=user.GroupCount;
            }
            else if (!string.IsNullOrWhiteSpace(username)) diagnostics["userFound"] = false;

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
