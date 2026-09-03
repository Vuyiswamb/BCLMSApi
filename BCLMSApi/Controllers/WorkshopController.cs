using System.Net;
using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkshopController(
    Datalayer datalayer,
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
            var requests = new List<WorkshopAttendanceRequestResponse>();
            await using var command = datalayer.CreateTextCommand(AttendanceRequestSelectSql + """

                WHERE requests.IsActive = 1
                ORDER BY
                    CASE requests.Status WHEN 'Requested' THEN 0 WHEN 'Date Set' THEN 1 WHEN 'Invited' THEN 2 WHEN 'Attended' THEN 3 ELSE 4 END,
                    requests.RequestedDate DESC;
                """);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                requests.Add(MapAttendanceRequest(reader));
            }

            return Ok(requests);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Unable to load workshop requests: {error.Message}" });
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
            var requests = new List<WorkshopAttendanceRequestResponse>();
            await using var command = datalayer.CreateTextCommand(AttendanceRequestSelectSql + """

                WHERE requests.IsActive = 1
                  AND requests.UserId = @UserId
                ORDER BY requests.RequestedDate DESC;
                """);
            command.Parameters.AddWithValue("@UserId", user.UserId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                requests.Add(MapAttendanceRequest(reader));
            }

            return Ok(requests);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Unable to load your workshop requests: {error.Message}" });
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

            await using var command = datalayer.CreateTextCommand("""
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.CustomerBusinesses
                    WHERE BusinessId = @BusinessId
                      AND UserId = @UserId
                      AND IsActive = 1
                )
                BEGIN
                    THROW 51001, 'Business not found for this customer.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.CustomerBusinesses
                    WHERE BusinessId = @BusinessId
                      AND COALESCE(WorkshopAttended, 0) = 1
                )
                BEGIN
                    THROW 51002, 'Workshop attendance has already been recorded for this business.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.WorkshopAttendanceRequests
                    WHERE BusinessId = @BusinessId
                      AND IsActive = 1
                      AND Status IN ('Requested', 'Date Set', 'Invited', 'Attended')
                )
                BEGIN
                    THROW 51003, 'A workshop request already exists for this business.', 1;
                END;

                INSERT dbo.WorkshopAttendanceRequests
                (
                    BusinessId,
                    UserId,
                    RequestedLicenceType,
                    Status,
                    RequestedDate,
                    Notes,
                    IsActive,
                    CreatedDate
                )
                VALUES
                (
                    @BusinessId,
                    @UserId,
                    @RequestedLicenceType,
                    'Requested',
                    SYSUTCDATETIME(),
                    @Notes,
                    1,
                    SYSUTCDATETIME()
                );

                SELECT CONVERT(INT, SCOPE_IDENTITY()) AS WorkshopAttendanceRequestId;
                """);
            command.Parameters.AddWithValue("@BusinessId", request.BusinessId);
            command.Parameters.AddWithValue("@UserId", user.UserId);
            command.Parameters.AddWithValue("@RequestedLicenceType", request.RequestedLicenceType.Trim());
            command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());

            await command.Connection!.OpenAsync();
            var requestId = Convert.ToInt32(await command.ExecuteScalarAsync());
            var created = await GetAttendanceRequestAsync(requestId);
            return created is null
                ? StatusCode(StatusCodes.Status500InternalServerError, new { message = "Workshop request was created but could not be loaded." })
                : Ok(created);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (SqlException error) when (error.Number is 51001 or 51002 or 51003)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (SqlException error)
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

        await using var command = datalayer.CreateTextCommand("""
            DECLARE @BusinessId INT;

            SELECT @BusinessId = BusinessId
            FROM dbo.WorkshopAttendanceRequests
            WHERE WorkshopAttendanceRequestId = @RequestId
              AND IsActive = 1;

            IF @BusinessId IS NULL
            BEGIN
                SELECT CONVERT(BIT, 0);
                RETURN;
            END;

            UPDATE dbo.WorkshopAttendanceRequests
            SET Status = 'Attended',
                AttendedDate = COALESCE(AttendedDate, SYSUTCDATETIME()),
                ModifiedDate = SYSUTCDATETIME()
            WHERE WorkshopAttendanceRequestId = @RequestId;

            UPDATE dbo.CustomerBusinesses
            SET WorkshopAttended = 1,
                WorkshopAttendedDate = COALESCE(WorkshopAttendedDate, SYSUTCDATETIME()),
                ModifiedDate = SYSUTCDATETIME()
            WHERE BusinessId = @BusinessId;

            SELECT CONVERT(BIT, 1);
            """);
        command.Parameters.AddWithValue("@RequestId", requestId);
        await command.Connection!.OpenAsync();
        var affected = Convert.ToBoolean(await command.ExecuteScalarAsync());
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

        try
        {
            await using var command = datalayer.CreateTextCommand("""
                UPDATE dbo.WorkshopAttendanceRequests
                SET ScheduledWorkshopDate = @ScheduledWorkshopDate,
                    Status = 'Date Set',
                    ModifiedDate = SYSUTCDATETIME()
                WHERE WorkshopAttendanceRequestId = @RequestId
                  AND IsActive = 1
                  AND Status <> 'Attended';

                SELECT @@ROWCOUNT;
                """);
            command.Parameters.AddWithValue("@RequestId", requestId);
            command.Parameters.AddWithValue("@ScheduledWorkshopDate", request.ScheduledWorkshopDate.Value.Date);
            await command.Connection!.OpenAsync();

            var affected = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (affected == 0)
            {
                return NotFound(new { message = "Workshop request not found or already attended." });
            }

            var updated = await GetAttendanceRequestAsync(requestId);
            return updated is null
                ? NotFound(new { message = "Workshop request not found." })
                : Ok(updated);
        }
        catch (SqlException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Unable to schedule workshop date: {error.Message}" });
        }
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
                var attendanceRequest = await GetAttendanceRequestAsync(attendanceRequestId);
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
                    await MarkRequestInvitedAsync(attendanceRequest.WorkshopAttendanceRequestId);
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

    private async Task MarkRequestInvitedAsync(int requestId)
    {
        await using var command = datalayer.CreateTextCommand("""
            UPDATE dbo.WorkshopAttendanceRequests
            SET Status = CASE WHEN Status = 'Requested' THEN 'Invited' ELSE Status END,
                InvitedDate = COALESCE(InvitedDate, SYSUTCDATETIME()),
                ModifiedDate = SYSUTCDATETIME()
            WHERE WorkshopAttendanceRequestId = @RequestId
              AND IsActive = 1;
            """);
        command.Parameters.AddWithValue("@RequestId", requestId);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    private async Task<WorkshopAttendanceRequestResponse?> GetAttendanceRequestAsync(int requestId)
    {
        await using var command = datalayer.CreateTextCommand(AttendanceRequestSelectSql + """

            WHERE requests.WorkshopAttendanceRequestId = @RequestId
              AND requests.IsActive = 1;
            """);
        command.Parameters.AddWithValue("@RequestId", requestId);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapAttendanceRequest(reader) : null;
    }

    private static WorkshopAttendanceRequestResponse MapAttendanceRequest(SqlDataReader reader)
    {
        return new WorkshopAttendanceRequestResponse
        {
            WorkshopAttendanceRequestId = reader.GetInt32(reader.GetOrdinal("WorkshopAttendanceRequestId")),
            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            RequestedLicenceType = reader.GetString(reader.GetOrdinal("RequestedLicenceType")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            RequestedDate = reader.GetDateTime(reader.GetOrdinal("RequestedDate")),
            ScheduledWorkshopDate = reader.IsDBNull(reader.GetOrdinal("ScheduledWorkshopDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ScheduledWorkshopDate")),
            InvitedDate = reader.IsDBNull(reader.GetOrdinal("InvitedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("InvitedDate")),
            AttendedDate = reader.IsDBNull(reader.GetOrdinal("AttendedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("AttendedDate")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
            OwnerName = reader.GetString(reader.GetOrdinal("OwnerName")),
            EmailAddress = reader.GetString(reader.GetOrdinal("EmailAddress")),
            MobileNumber = reader.GetString(reader.GetOrdinal("MobileNumber")),
            TownshipName = reader.IsDBNull(reader.GetOrdinal("TownshipName")) ? null : reader.GetString(reader.GetOrdinal("TownshipName"))
        };
    }

    private const string AttendanceRequestSelectSql = """
        SELECT
            requests.WorkshopAttendanceRequestId,
            requests.BusinessId,
            requests.UserId,
            requests.RequestedLicenceType,
            requests.Status,
            requests.RequestedDate,
            requests.ScheduledWorkshopDate,
            requests.InvitedDate,
            requests.AttendedDate,
            requests.Notes,
            businesses.BusinessName,
            COALESCE(NULLIF(users.DisplayName, ''), users.Username) AS OwnerName,
            COALESCE(NULLIF(users.EmailAddress, ''), users.Username) AS EmailAddress,
            COALESCE(latestApplication.MobileNumber, '') AS MobileNumber,
            townships.TOWNSHIP AS TownshipName
        FROM dbo.WorkshopAttendanceRequests requests
        INNER JOIN dbo.CustomerBusinesses businesses ON businesses.BusinessId = requests.BusinessId
        INNER JOIN dbo.Users users ON users.UserId = requests.UserId
        LEFT JOIN dbo.TOWNSHIPS townships ON townships.ID = businesses.TownshipId
        OUTER APPLY
        (
            SELECT TOP (1) applications.MobileNumber
            FROM dbo.Applications applications
            WHERE applications.BusinessId = businesses.BusinessId
              AND applications.Archive_Date IS NULL
            ORDER BY applications.SubmittedDate DESC, applications.ApplicationId DESC
        ) latestApplication
        """;

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
