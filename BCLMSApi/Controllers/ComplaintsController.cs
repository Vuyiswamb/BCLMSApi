using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComplaintsController(IComplaintService complaintService, IUserTokenService userTokenService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ComplaintResponse>> CreateComplaint(ComplaintCreateRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to submit a complaint." });
        }

        try
        {
            return Ok(await complaintService.CreateComplaintAsync(user, request));
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<ComplaintResponse>>> GetMyComplaints()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to view complaints." });
        }

        try
        {
            return Ok(await complaintService.GetCustomerComplaintsAsync(user));
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpGet("internal")]
    public async Task<ActionResult<List<ComplaintResponse>>> GetInternalComplaints()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to view complaints." });
        }

        try
        {
            return Ok(await complaintService.GetInternalComplaintsAsync(user));
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPut("internal/{complaintId:int}/status")]
    public async Task<ActionResult<ComplaintResponse>> UpdateComplaintStatus(int complaintId, ComplaintStatusUpdateRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to update complaints." });
        }

        try
        {
            var complaint = await complaintService.UpdateComplaintStatusAsync(user, complaintId, request);
            return complaint is null
                ? NotFound(new { message = "Complaint not found." })
                : Ok(complaint);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }

    [HttpPost("{complaintId:int}/cancel")]
    public async Task<ActionResult<ComplaintResponse>> CancelComplaint(int complaintId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to cancel a complaint." });
        }

        try
        {
            var complaint = await complaintService.CancelComplaintAsync(user, complaintId);
            return complaint is null
                ? NotFound(new { message = "Complaint not found or cannot be cancelled." })
                : Ok(complaint);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.Message });
        }
    }
}
