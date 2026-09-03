using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicantAuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ManagedUserResponse>> Register(RegisterCustomerRequest request)
    {
        try
        {
            var user = await authService.RegisterCustomerAsync(request);
            return Ok(new { message = "Registration completed. You can now log in.", user });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Conflict(new { message = error.Message });
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The database is currently unavailable. Please try again shortly." });
        }
    }
}
