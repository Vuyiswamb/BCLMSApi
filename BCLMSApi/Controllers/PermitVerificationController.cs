using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BCLMSApi.Data;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/PermitVerification")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PermitVerificationController(Datalayer datalayer, IFormalBusinessRepository repository,
    IFormalBusinessService applicationService, IUserTokenService tokens, IConfiguration configuration) : ControllerBase
{
    private static (DateTime? Start, DateTime? End) ValidityDates(InternalApplicationDetailResponse application)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(application.LicenceType, "events? licen[cs]e", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            if (!application.EventStartDate.HasValue || !application.EventEndDate.HasValue || application.EventEndDate < application.EventStartDate)
                return (null, null);
            return (application.EventStartDate.Value.Date, application.EventEndDate.Value.Date);
        }
        var issued = IssueDate(application);
        return (issued, issued?.AddYears(1).AddDays(-1));
    }

    [HttpGet("applications/{applicationId:int}/link")]
    public async Task<IActionResult> GetLink(int applicationId)
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return Unauthorized(new { message = "Please sign in again." });
        var application = await applicationService.GetInternalApplicationDetailAsync(applicationId, user);
        if (application is null || !IsPermit(application)) return NotFound();
        var (issued, expires) = ValidityDates(application);
        var isEvent = System.Text.RegularExpressions.Regex.IsMatch(application.LicenceType, "events? licen[cs]e", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!IsCompleted(application.Status) || (!isEvent && (issued is null || expires is null)))
            return Conflict(new { message = "The permit must have completed approval and a recorded approval date before it can be downloaded." });
        return Ok(new { token = CreateToken(applicationId), validFrom = issued, validUntil = expires });
    }

    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<IActionResult> Verify(string token)
    {
        var application = await FindPermit(token);
        if (application is null) return NotFound(new { message = "This permit could not be verified. Check the QR code or contact the issuing office." });
        var (issued, expires) = ValidityDates(application);

        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).Date;
        var status = !IsCompleted(application.Status) ? "Not valid"
            : issued is null || expires is null ? "Unable to confirm validity"
            : today < issued ? "Not yet valid" : today > expires ? "Expired" : "Valid";
        var stalls = new List<object>();
        await using (var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Stalls_GetAll"))
        {
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!string.Equals(reader["AllocatedTrackingNumber"] as string, application.TrackingNumber, StringComparison.OrdinalIgnoreCase)) continue;
                stalls.Add(new { stallNumber = reader["StallNumber"], stallName = reader["StallName"],
                    address = reader["Address"], latitude = reader["Latitude"], longitude = reader["Longitude"] });
            }
        }
        return Ok(new { permitNumber = application.TrackingNumber, holderName = application.ApplicantName,
            businessName = application.BusinessName, licenceType = application.LicenceType, status, isValid = status == "Valid", validFrom = issued,
            validUntil = expires, address = application.PhysicalAddress, latitude = application.Latitude,
            longitude = application.Longitude, stalls, photoAvailable = await ReadPhoto(application.ApplicationId, includeContent: false) is not null });
    }

    [AllowAnonymous]
    [HttpGet("{token}/photo")]
    public async Task<IActionResult> GetPhoto(string token)
    {
        var application = await FindPermit(token);
        if (application is null) return NotFound();
        var photo = await ReadPhoto(application.ApplicationId, includeContent: true);
        if (photo?.Content is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.Value.Content, photo.Value.ContentType);
    }

    [HttpPost("applications/{applicationId:int}/photo")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(int applicationId, IFormFile photo)
    {
        var user = tokens.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return Unauthorized(new { message = "Please sign in again." });
        if (!user.Groups.Any(group => new[] { "Super User", "Admin Officer", "Director", "Functional Head" }.Contains(group, StringComparer.OrdinalIgnoreCase)))
            return StatusCode(403, new { message = "Permit management access is required." });
        var application = await applicationService.GetInternalApplicationDetailAsync(applicationId, user);
        if (application is null || !IsPermit(application)) return NotFound();
        if (photo.Length <= 0 || photo.Length > 2 * 1024 * 1024)
            return BadRequest(new { message = "Select a JPEG or PNG photo up to 2 MB." });
        using var stream = new MemoryStream();
        await photo.CopyToAsync(stream);
        var bytes = stream.ToArray();
        var contentType = bytes.Length >= 8 && bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
            : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg" : null;
        if (contentType is null) return BadRequest(new { message = "Only JPEG and PNG photos are supported." });
        await using var command = datalayer.CreateTextCommand("""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DECLARE @LockResult INT;
            EXEC @LockResult = sys.sp_getapplock @Resource = 'PermitHolderPhotos', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
            IF @LockResult < 0 THROW 50001, 'Unable to save the permit photo. Please retry.', 1;
            IF OBJECT_ID('dbo.PermitHolderPhotos', 'U') IS NULL
                CREATE TABLE dbo.PermitHolderPhotos (
                    ApplicationId INT NOT NULL PRIMARY KEY REFERENCES dbo.Applications(ApplicationId),
                    ContentType NVARCHAR(50) NOT NULL, FileContent VARBINARY(MAX) NOT NULL,
                    UpdatedByUserId INT NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
            UPDATE dbo.PermitHolderPhotos SET ContentType = @ContentType, FileContent = @FileContent,
                UpdatedByUserId = @UserId, UpdatedAtUtc = SYSUTCDATETIME() WHERE ApplicationId = @ApplicationId;
            IF @@ROWCOUNT = 0 INSERT INTO dbo.PermitHolderPhotos VALUES (@ApplicationId, @ContentType, @FileContent, @UserId, SYSUTCDATETIME());
            COMMIT;
            """);
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.AddWithValue("@UserId", user.UserId);
        command.Parameters.AddWithValue("@ContentType", contentType);
        command.Parameters.Add("@FileContent", SqlDbType.VarBinary, -1).Value = bytes;
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
        return Ok(new { message = "Permit holder photo saved. It will appear when the permit QR code is scanned." });
    }

    // Keep availability and delivery on the same lookup, scoped to the signed application.
    // An explicitly uploaded permit photo takes precedence over its linked business photo.
    private async Task<(string ContentType, byte[]? Content)?> ReadPhoto(int applicationId, bool includeContent)
    {
        await using var command = datalayer.CreateTextCommand("""
            DECLARE @Found BIT = 0;
            IF OBJECT_ID('dbo.PermitHolderPhotos', 'U') IS NOT NULL
                EXEC sys.sp_executesql N'
                    IF EXISTS (SELECT 1 FROM dbo.PermitHolderPhotos WHERE ApplicationId = @Id AND DATALENGTH(FileContent) > 0)
                    BEGIN
                        SELECT ContentType, CASE WHEN @Include = 1 THEN FileContent ELSE NULL END AS FileContent
                        FROM dbo.PermitHolderPhotos WHERE ApplicationId = @Id;
                        SET @Found = 1;
                    END',
                    N'@Id INT, @Include BIT, @Found BIT OUTPUT',
                    @Id = @ApplicationId, @Include = @IncludeContent, @Found = @Found OUTPUT;
            IF @Found = 1 RETURN;

            SELECT COALESCE(NULLIF(business.BusinessPhotoContentType, ''), 'image/jpeg') AS ContentType,
                CASE WHEN @IncludeContent = 1 THEN business.BusinessPhotoContent ELSE NULL END AS FileContent
            FROM dbo.Applications AS application
            INNER JOIN dbo.CustomerBusinesses AS business ON business.BusinessId = application.BusinessId
            WHERE application.ApplicationId = @ApplicationId
                AND application.Archive_Date IS NULL
                AND DATALENGTH(business.BusinessPhotoContent) > 0;
            """);
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.Add("@IncludeContent", SqlDbType.Bit).Value = includeContent;
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetString(0), includeContent ? reader.GetFieldValue<byte[]>(1) : null);
    }

    private async Task<InternalApplicationDetailResponse?> FindPermit(string token)
    {
        if (token.Length > 100) return null;
        var parts = token.Split('.');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(CreateToken(id)))) return null;
        var application = await repository.GetInternalApplicationDetailAsync(id);
        return application is not null && IsPermit(application) ? application : null;
    }

    private string CreateToken(int id)
    {
        var key = Convert.FromBase64String(configuration["PasswordSecurity:Pepper"] ?? throw new InvalidOperationException("Token signing key is missing."));
        var number = id.ToString(CultureInfo.InvariantCulture);
        var signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("permit-verification:v1:" + number));
        return number + "." + Convert.ToBase64String(signature).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsPermit(InternalApplicationDetailResponse application) => !string.IsNullOrWhiteSpace(application.LicenceType);
    private static bool IsCompleted(string status) => new[] { "completed", "complete" }.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);
    private static DateTime? IssueDate(InternalApplicationDetailResponse application) => application.WorkflowSteps
        .Where(step => IsCompleted(step.Status) || step.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        .Select(step => step.CompletedDate ?? step.DecisionDate).Max()?.Date;
}
