using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/reception-visits")]
public sealed class ReceptionVisitsController(ReceptionVisitRepository repository, IUserTokenService tokens) : ControllerBase
{
    private UserTokenPayload Staff()
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if (!AssistedIntakeController.CanAssist(user)) throw new UnauthorizedAccessException("Reception access is required.");
        return user!;
    }

    [HttpGet]
    public async Task<ActionResult<List<ReceptionVisitResponse>>> List()
    {
        try { Staff(); return Ok(await repository.ListAsync()); }
        catch (UnauthorizedAccessException error) { return StatusCode(403, new { message = error.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<ReceptionVisitResponse>> Create(ReceptionVisitCreateRequest request)
    {
        try
        {
            var user = Staff();
            var serviceOptions = ReceptionServices.Options;
            if (request.VisitDate == default || request.VisitDate.Date > DateTime.UtcNow.Date.AddDays(1)) return BadRequest(new { message = "Enter a valid visit date." });
            if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150) return BadRequest(new { message = "Surname and names are required." });
            if (string.IsNullOrWhiteSpace(request.ContactNumber) || request.ContactNumber.Trim().Length > 40) return BadRequest(new { message = "A contact number is required." });
            if (!serviceOptions.Contains(request.ServiceRequired.Trim(), StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Select a valid service required option." });
            if (!string.IsNullOrWhiteSpace(request.Referral) && request.Referral.Trim().Length > 200) return BadRequest(new { message = "Referral may not exceed 200 characters." });
            return Ok(await repository.CreateAsync(request, user.UserId));
        }
        catch (UnauthorizedAccessException error) { return StatusCode(403, new { message = error.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            Staff();
            if (id <= 0) return BadRequest(new { message = "Invalid reception visit." });
            return await repository.DeleteAsync(id) ? NoContent() : NotFound(new { message = "Reception visit not found." });
        }
        catch (UnauthorizedAccessException error) { return StatusCode(403, new { message = error.Message }); }
    }
}

public static class ReceptionServices
{
    public static readonly string[] Options = ["Enquiry", "Payment", "Renewal", "New application", "Follow up", "Attending workshops", "Submitting documents"];
}
