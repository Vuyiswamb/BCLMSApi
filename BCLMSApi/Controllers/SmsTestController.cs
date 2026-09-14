using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/Settings/sms")]
public class SmsTestController(ISmsService smsService, IUserTokenService userTokenService) : ControllerBase
{
    [HttpPost("test")]
    public async Task<IActionResult> SendTest(SmsTestRequest request)
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Super User access is required to send a test SMS." });



        try
        {
            await smsService.SendSmsAsync(request.CellphoneNumber, "BCLMS test SMS. Your SMS service is working.");
            return Ok(new { message = "Test SMS accepted by Vodacom. Check your cellphone to confirm delivery." });
        }
        catch (ArgumentException error) 
        {
            return BadRequest(new { message = error.Message }); 
        }
        catch (InvalidOperationException error) 
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = error.Message });
        }
    }
}

public class SmsTestRequest
{
    [Required, StringLength(30)]
    public string CellphoneNumber { get; set; } = string.Empty;
}
