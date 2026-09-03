using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserManagementController(IAuthService authService, IUserTokenService userTokenService) : ControllerBase
{
    [HttpGet("groups")]
    public async Task<ActionResult<List<GroupResponse>>> GetGroups()
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to load permission groups." });
        }

        try
        {
            return Ok(await authService.GetGroupsAsync(includeCustomer: false));
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The database is currently unavailable. Permission groups could not be loaded." });
        }
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<ManagedUserResponse>>> GetUsers()
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to load users." });
        }

        try
        {
            return Ok(await authService.GetUsersAsync());
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The database is currently unavailable. Users could not be loaded." });
        }
    }

    [HttpPost("officials")]
    public async Task<ActionResult<ManagedUserResponse>> CreateOfficialUser(CreateOfficialUserRequest request)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to create officials." });
        }

        try
        {
            return Ok(await authService.CreateOfficialUserAsync(request));
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

    [HttpPut("users/{userId:int}")]
    public async Task<ActionResult<ManagedUserResponse>> UpdateOfficialUser(int userId, UpdateOfficialUserRequest request)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to edit officials." });
        }

        try
        {
            return Ok(await authService.UpdateOfficialUserAsync(userId, request));
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Conflict(new { message = error.Message });
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The database is currently unavailable. The official could not be updated." });
        }
    }

    [HttpPost("users/{userId:int}/reset-password")]
    public async Task<ActionResult<PasswordResetByAdminResponse>> ResetUserPassword(int userId)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to reset passwords." });
        }

        try
        {
            return Ok(await authService.ResetUserPasswordByAdminAsync(userId));
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Conflict(new { message = error.Message });
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The database is currently unavailable. The password could not be reset." });
        }
    }

    [HttpPost("users/{userId:int}/set-password")]
    public async Task<ActionResult<PasswordResetByAdminResponse>> SetUserPassword(int userId, SetUserPasswordByAdminRequest request)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to set passwords." });
        }

        try
        {
            return Ok(await authService.SetUserPasswordByAdminAsync(userId, request));
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Conflict(new { message = error.Message });
        }
        catch (SqlException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The database is currently unavailable. The password could not be set." });
        }
    }
}
