using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/workflow-templates")]
public sealed class WorkflowTemplatesController(WorkflowTemplateRepository repository, IUserTokenService userTokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WorkflowTemplateResponse>>> GetAll()
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        return Ok(await repository.GetAllAsync());
    }

    [HttpGet("application-types")]
    public async Task<ActionResult<List<string>>> GetApplicationTypes()
    {
        if (!userTokenService.IsSuperUserToken(Request.Headers.Authorization)) return Forbid();
        return Ok(await repository.GetApplicationTypesAsync());
    }

    [HttpPut]
    public async Task<ActionResult<WorkflowTemplateResponse>> Save(WorkflowTemplateSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || !user.IsSuperUser) return Forbid();
        if (string.IsNullOrWhiteSpace(request.ApplicationType) || string.IsNullOrWhiteSpace(request.TemplateName) || request.Steps.Count == 0)
            return BadRequest(new { message = "Application type, template name and at least one workflow step are required." });
        if (request.Steps.Any(step => string.IsNullOrWhiteSpace(step.StepName) || string.IsNullOrWhiteSpace(step.AssignedGroupName)))
            return BadRequest(new { message = "Every workflow step must have a name and assigned group." });
        return Ok(await repository.SaveAsync(request, user.UserId));
    }
}
