using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ISystemSettingsService settingsService, IUserTokenService userTokenService) : ControllerBase
{
    [HttpGet("email")]
    public async Task<ActionResult<EmailSettingsResponse>> GetEmailSettings()
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to view settings." });
        }

        return Ok(await settingsService.GetEmailSettingsAsync());
    }

    [HttpPut("email")]
    public async Task<ActionResult<EmailSettingsResponse>> UpdateEmailSettings(EmailSettingsUpdateRequest request)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to update settings." });
        }

        return Ok(await settingsService.UpdateEmailSettingsAsync(request));
    }

    [HttpGet("hanis")]
    public async Task<ActionResult<HanisSettingsResponse>> GetHanisSettings()
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to view settings." });
        return Ok(await settingsService.GetHanisSettingsAsync());
    }

    [HttpPut("hanis")]
    public async Task<ActionResult<HanisSettingsResponse>> UpdateHanisSettings(HanisSettingsUpdateRequest request)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to update settings." });
        return Ok(await settingsService.UpdateHanisSettingsAsync(request));
    }
}
