using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/HomeAffairs")]
public class HomeAffairsController(IFormalBusinessService applications, IUserTokenService tokens,
    HomeAffairsLookupStore lookups, ILogger<HomeAffairsController> logger, IHostEnvironment environment) : ControllerBase
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
        try
        {
            var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
            if (user is null)
            {
                Response.Headers.WWWAuthenticate = "Bearer";
                return Unauthorized(new { message = "A valid, unexpired bearer token is required." });
            }
            if (user.IsCustomer)
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
                var result = await lookups.GetAsync(application, user, refresh, savedOnly);
            return result is null ? NoContent() : Ok(result);
        }
        catch (ArgumentException error)
        {
            if (error.InnerException is not null)
                logger.LogError(error, "Home Affairs lookup failed for application {ApplicationId}", applicationId);
            return LookupError(400, error.Message, error);
        }
        catch (Microsoft.Data.SqlClient.SqlException error)
        {
            logger.LogError(error, "Home Affairs lookup storage failed for application {ApplicationId}; SQL error {ErrorNumber}", applicationId, error.Number);
            return LookupError(503, "Unable to load or save the Home Affairs lookup. Please try again; the workflow has not changed.", error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected Home Affairs lookup failure for application {ApplicationId}", applicationId);
            return LookupError(500, "Home Affairs lookup failed. Please try again; the workflow has not changed.", ex);
        }
    }

    private ObjectResult LookupError(int status, string message, Exception ex)
    {
        if (environment.IsDevelopment())
            return StatusCode(status, new
            {
                message = $"{message} Cause: {ex.GetBaseException().Message}",
                exceptionType = ex.GetBaseException().GetType().FullName,
                details = ex.ToString(),
                traceId = HttpContext.TraceIdentifier
            });
        return StatusCode(status, new { message, traceId = HttpContext.TraceIdentifier });
    }
}
