using System.Net;
using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkshopController(
    WorkshopRepository workshopRepository,
    IEmailService emailService,
    ISmsService smsService,
    IUserTokenService userTokenService) : ControllerBase
{
    private const int SmsMaxLength = 130;

    [HttpGet("attendance-requests")]
    public async Task<ActionResult<List<WorkshopAttendanceRequestResponse>>> GetAttendanceRequests()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || user.IsCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Internal access is required to manage workshop attendance." });
        }

        try
        {
            return Ok(await workshopRepository.GetInternalAsync());
        }
        catch (Exception error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Unable to load workshop requests: {error.Message}" });
        }
    }

    [HttpGet("attendance-requests/mine")]
    public async Task<ActionResult<List<WorkshopAttendanceRequestResponse>>> GetMyAttendanceRequests()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return Unauthorized(new { message = "Login is required to view workshop attendance." });
        }

        try
        {
            return Ok(await workshopRepository.GetMineAsync(user.UserId));
        }
        catch (Exception error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Unable to load your workshop requests: {error.Message}" });
        }
    }

    [HttpPost("attendance-requests")]
    public async Task<ActionResult<WorkshopAttendanceRequestResponse>> CreateAttendanceRequest(WorkshopAttendanceRequestCreateRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return Unauthorized(new { message = "Login is required to request workshop training." });
        }

        try
        {
            ValidateCreateRequest(request);

            var requestId = await workshopRepository.CreateAsync(request,user.UserId);
            var created = await workshopRepository.GetByIdAsync(requestId);
            return created is null
                ? StatusCode(StatusCodes.Status500InternalServerError, new { message = "Workshop request was created but could not be loaded." })
                : Ok(created);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (WorkshopRepositoryException error) when (error.Number is 51001 or 51002 or 51003)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (WorkshopRepositoryException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Workshop request database error: {error.Message}" });
        }
        catch (Exception error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Workshop request failed: {error.Message}" });
        }
    }

    [HttpPost("attendance-requests/{requestId:int}/attendance")]
    public async Task<ActionResult<object>> MarkRequestAttendance(int requestId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || user.IsCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Internal access is required to manage workshop attendance." });
        }

        var affected = await workshopRepository.MarkAttendedAsync(requestId);
        return affected
            ? Ok(new { message = "Workshop attendance recorded." })
            : NotFound(new { message = "Workshop request not found." });
    }

    [HttpPost("attendance-requests/{requestId:int}/schedule")]
    public async Task<ActionResult<WorkshopAttendanceRequestResponse>> ScheduleWorkshop(int requestId, WorkshopAttendanceScheduleRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || user.IsCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Internal access is required to schedule workshop attendance." });
        }

        if (request.ScheduledWorkshopDate is null)
        {
            return BadRequest(new { message = "Select a workshop date." });
        }

        var affected = await workshopRepository.ScheduleAsync(requestId,request.ScheduledWorkshopDate.Value);
            if (!affected)
            {
                return NotFound(new { message = "Workshop request not found or already attended." });
            }

            var updated = await workshopRepository.GetByIdAsync(requestId);
            return updated is null
                ? NotFound(new { message = "Workshop request not found." })
                : Ok(updated);
    }

    [HttpPost("invitations")]
    public async Task<ActionResult<WorkshopInvitationResponse>> SendInvitations(WorkshopInvitationRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null || user.IsCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Internal access is required to invite applicants to workshops." });
        }

        try
        {
            ValidateRequest(request);
            var requestIds = request.RequestIds.Where(id => id > 0).Distinct().ToList();
            var response = new WorkshopInvitationResponse { TotalApplicants = requestIds.Count };

            foreach (var attendanceRequestId in requestIds)
            {
                var attendanceRequest = await workshopRepository.GetByIdAsync(attendanceRequestId);
                if (attendanceRequest is null)
                {
                    response.Recipients.Add(new WorkshopInvitationRecipientResult { WorkshopAttendanceRequestId = attendanceRequestId, Error = "Workshop request not found." });
                    continue;
                }

                var result = new WorkshopInvitationRecipientResult
                {
                    WorkshopAttendanceRequestId = attendanceRequest.WorkshopAttendanceRequestId,
                    BusinessId = attendanceRequest.BusinessId,
                    ApplicantName = attendanceRequest.OwnerName,
                    BusinessName = attendanceRequest.BusinessName,
                    EmailAddress = attendanceRequest.EmailAddress,
                    MobileNumber = attendanceRequest.MobileNumber
                };

                if (request.SendEmail && !string.IsNullOrWhiteSpace(attendanceRequest.EmailAddress))
                {
                    try
                    {
                        await emailService.SendEmailOffice365Async(
                            attendanceRequest.EmailAddress,
                            request.EmailSubject.Trim(),
                            BuildEmailBody(attendanceRequest.OwnerName, attendanceRequest.BusinessName, request.EmailBody));
                        result.EmailSent = true;
                        response.EmailSent++;
                    }
                    catch (Exception error)
                    {
                        result.Error = error.Message;
                    }
                }

                if (request.SendSms && !string.IsNullOrWhiteSpace(attendanceRequest.MobileNumber))
                {
                    try
                    {
                        await smsService.SendSmsAsync(attendanceRequest.MobileNumber, request.SmsMessage.Trim());
                        result.SmsSent = true;
                        response.SmsSent++;
                    }
                    catch (Exception error)
                    {
                        result.Error = string.IsNullOrWhiteSpace(result.Error) ? error.Message : $"{result.Error} SMS: {error.Message}";
                    }
                }

                if (result.EmailSent || result.SmsSent)
                {
                    await workshopRepository.MarkInvitedAsync(attendanceRequest.WorkshopAttendanceRequestId);
                }

                response.Recipients.Add(result);
            }

            return Ok(response);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }

    private static void ValidateCreateRequest(WorkshopAttendanceRequestCreateRequest request)
    {
        if (request.WorkshopMode is not ("Online" or "Physical"))
            throw new ArgumentException("Choose Online or Physical workshop attendance.");
        if (request.BusinessId <= 0)
        {
            throw new ArgumentException("Select a business before requesting workshop training.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestedLicenceType))
        {
            throw new ArgumentException("Select the non-Formal licence type for the workshop request.");
        }

        if (request.RequestedLicenceType.Contains("Formal Business", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Workshop training is only required for non-Formal application types.");
        }
    }

    private static void ValidateRequest(WorkshopInvitationRequest request)
    {
        if (request.RequestIds.Count == 0)
        {
            throw new ArgumentException("Select at least one workshop request.");
        }

        if (!request.SendEmail && !request.SendSms)
        {
            throw new ArgumentException("Select email, SMS, or both.");
        }

        if (request.SendEmail && (string.IsNullOrWhiteSpace(request.EmailSubject) || string.IsNullOrWhiteSpace(request.EmailBody)))
        {
            throw new ArgumentException("Email subject and body are required.");
        }

        if (request.SendSms && string.IsNullOrWhiteSpace(request.SmsMessage))
        {
            throw new ArgumentException("SMS text is required.");
        }

        if ((request.SmsMessage?.Trim().Length ?? 0) > SmsMaxLength)
        {
            throw new ArgumentException($"SMS text may not exceed {SmsMaxLength} characters.");
        }
    }

    

    private static string BuildEmailBody(string applicantName, string businessName, string preparedBody)
    {
        var body = WebUtility.HtmlEncode(preparedBody.Trim())
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);

        return $@"
<!DOCTYPE html>
<html lang='en'>
<body style='font-family:Arial,Helvetica,sans-serif;background:#edf2f5;margin:0;padding:24px;'>
  <div style='max-width:680px;margin:0 auto;background:#fff;border:1px solid #d8e2e8;border-radius:8px;overflow:hidden;'>
    <div style='padding:24px 32px;border-bottom:4px solid #16821f;'>
      <p style='margin:0;color:#16821f;font-weight:bold;text-transform:uppercase;font-size:12px;'>City of Tshwane Business Compliance & Licensing</p>
      <h1 style='margin:10px 0 0;color:#102033;font-size:24px;'>Workshop invitation</h1>
    </div>
    <div style='padding:32px;color:#102033;line-height:1.65;'>
      <p style='margin:0 0 18px;'>Dear {WebUtility.HtmlEncode(applicantName)},</p>
      <p style='margin:0 0 18px;'>This invitation relates to <strong>{WebUtility.HtmlEncode(businessName)}</strong>.</p>
      <p style='margin:0;'>{body}</p>
    </div>
  </div>
</body>
</html>";
    }
}
