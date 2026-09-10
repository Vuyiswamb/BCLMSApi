using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/HomeAffairs")]
public class HomeAffairsController(IFormalBusinessService applications, IUserTokenService tokens,
    HomeAffairsLookupStore lookups, ILogger<HomeAffairsController> logger) : ControllerBase
{
    [HttpGet("applications/{applicationId:int}/lookup")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Saved(int applicationId) => LookupCore(applicationId, false, true);

    [HttpPost("applications/{applicationId:int}/lookup")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Lookup(int applicationId, [FromQuery] bool refresh = false)
        => LookupCore(applicationId, refresh, false);

    private async Task<IActionResult> LookupCore(int applicationId, bool refresh, bool savedOnly)
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || user.IsCustomer)
            return StatusCode(403, new { message = "Only assigned municipal officials may look up an applicant's identity." });
        if (applicationId <= 0) return BadRequest(new { message = "Select a valid application." });
        var application = await applications.GetInternalApplicationDetailAsync(applicationId, user);
        if (application is null) return NotFound(new { message = "Application not found or unavailable to you." });
        var step = application.WorkflowSteps.OrderBy(s => s.SequenceNumber)
            .FirstOrDefault(s => !new[] { "Approved", "Completed", "Complete" }.Contains(s.Status.Trim(), StringComparer.OrdinalIgnoreCase));
        if (step is null || !step.StepName.Trim().Equals("Home Affairs Verification", StringComparison.OrdinalIgnoreCase)
            || !new[] { "Pending", "In progress", "Submitted", "Under Review", "Rejected" }.Contains(step.Status.Trim(), StringComparer.OrdinalIgnoreCase)
            || new[] { "Cancelled", "Canceled", "Completed", "Archived" }.Contains(application.Status.Trim(), StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Identity lookup is available during the Home Affairs Verification step." });
        if (!user.IsSuperUser && !user.Groups.Contains(step.GroupName, StringComparer.OrdinalIgnoreCase))
            return StatusCode(403, new { message = "You are not assigned to the Home Affairs Verification step." });
        try
        {
            var result = await lookups.GetAsync(application, user, refresh, savedOnly);
            return result is null ? NoContent() : Ok(result);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (Microsoft.Data.SqlClient.SqlException error)
        {
            logger.LogError("Home Affairs lookup storage failed for application {ApplicationId}; SQL error {ErrorNumber}", applicationId, error.Number);
            return StatusCode(503, new { message = "Unable to load or save the Home Affairs lookup. Please try again; the workflow has not changed." });
        }
    }
}
