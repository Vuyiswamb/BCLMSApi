using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [HttpGet("database")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult CheckHealth()
    {
        return Ok(new { success = true, message = "We are good." });
    }
}
