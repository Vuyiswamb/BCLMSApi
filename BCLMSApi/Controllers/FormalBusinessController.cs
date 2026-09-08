using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BCLMSApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FormalBusinessController(IFormalBusinessService formalBusinessService, IUserTokenService userTokenService) : ControllerBase
{
    private const long MaxPdfBytes = 10 * 1024 * 1024;
    private const long MultipartRequestBytes = MaxPdfBytes + 1 * 1024 * 1024;

    [HttpGet("new-application-template")]
    public ActionResult<FormalBusinessApplicationTemplate> GetNewApplicationTemplate()
    {
        return Ok(formalBusinessService.GetNewApplicationTemplate());
    }

    [HttpGet("attachment-types")]
    public async Task<ActionResult<List<AttachmentTypeResponse>>> GetAttachmentTypes()
    {
        return Ok(await formalBusinessService.GetAttachmentTypesAsync());
    }

    [HttpGet("townships")]
    public async Task<ActionResult<List<TownshipResponse>>> GetTownships()
    {
        return Ok(await formalBusinessService.GetTownshipsAsync());
    }

    [HttpGet("tariffs")]
    public async Task<ActionResult<TariffResponse>> GetTariff([FromQuery] string licenceType, [FromQuery] string applicationKind = "New")
    {
        try
        {
            var tariff = await formalBusinessService.GetTariffAsync(licenceType, applicationKind);
            return tariff is null
                ? NotFound(new { message = "No tariff is configured for the selected application type." })
                : Ok(tariff);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpGet("permit-rental-fee")]
    public async Task<ActionResult<PermitRentalFeeResponse>> GetPermitRentalFee([FromQuery] string businessType, [FromQuery] string? tradingLocation)
    {
        try
        {
            var fee = await formalBusinessService.GetPermitRentalFeeAsync(businessType, tradingLocation);
            return fee is null
                ? NotFound(new { message = "No monthly permit rental fee is configured for this business type and location." })
                : Ok(fee);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }

    [HttpGet("tariffs/internal")]
    public async Task<ActionResult<List<TariffResponse>>> GetTariffs()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to load application prices." });
        }

        try
        {
            return Ok(await formalBusinessService.GetTariffsAsync(user));
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpGet("permit-rental-fees/internal")]
    public async Task<ActionResult<List<PermitRentalFeeResponse>>> GetPermitRentalFees()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to load permit rental fees." });
        try { return Ok(await formalBusinessService.GetPermitRentalFeesAsync(user)); }
        catch (UnauthorizedAccessException error) { return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message }); }
    }

    [HttpPost("permit-rental-fees")]
    public async Task<ActionResult<PermitRentalFeeResponse>> SavePermitRentalFee(PermitRentalFeeSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to save permit rental fees." });
        try { return Ok(await formalBusinessService.SavePermitRentalFeeAsync(user, request)); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (UnauthorizedAccessException error) { return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message }); }
    }

    [HttpPut("permit-rental-fees/{permitRentalFeeId:int}")]
    public async Task<ActionResult<PermitRentalFeeResponse>> UpdatePermitRentalFee(int permitRentalFeeId, PermitRentalFeeSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to update permit rental fees." });
        try { var fee = await formalBusinessService.UpdatePermitRentalFeeAsync(user, permitRentalFeeId, request); return fee is null ? NotFound() : Ok(fee); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (UnauthorizedAccessException error) { return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message }); }
    }

    [HttpPost("permit-rental-fees/{permitRentalFeeId:int}/disable")]
    public async Task<ActionResult> DisablePermitRentalFee(int permitRentalFeeId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return StatusCode(StatusCodes.Status403Forbidden);
        try { return await formalBusinessService.DisablePermitRentalFeeAsync(user, permitRentalFeeId) ? Ok() : NotFound(); }
        catch (UnauthorizedAccessException error) { return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message }); }
    }

    [HttpDelete("permit-rental-fees/{permitRentalFeeId:int}")]
    public async Task<ActionResult> DeletePermitRentalFee(int permitRentalFeeId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null) return StatusCode(StatusCodes.Status403Forbidden);
        try { return await formalBusinessService.DeletePermitRentalFeeAsync(user, permitRentalFeeId) ? Ok() : NotFound(); }
        catch (UnauthorizedAccessException error) { return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message }); }
    }

    [HttpPost("tariffs")]
    public async Task<ActionResult<TariffResponse>> SaveTariff(TariffSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to save application prices." });
        }

        try
        {
            return Ok(await formalBusinessService.SaveTariffAsync(user, request));
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
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpPut("tariffs/{tariffId:int}")]
    public async Task<ActionResult<TariffResponse>> UpdateTariff(int tariffId, TariffSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to update application prices." });
        }

        try
        {
            var tariff = await formalBusinessService.UpdateTariffAsync(user, tariffId, request);
            return tariff is null ? NotFound(new { message = "Application price not found." }) : Ok(tariff);
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
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpPost("tariffs/{tariffId:int}/disable")]
    public async Task<ActionResult> DisableTariff(int tariffId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to disable application prices." });
        }

        try
        {
            return await formalBusinessService.DisableTariffAsync(user, tariffId)
                ? Ok(new { message = "Application price disabled." })
                : NotFound(new { message = "Application price not found." });
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
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpDelete("tariffs/{tariffId:int}")]
    public async Task<ActionResult> DeleteTariff(int tariffId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to delete application prices." });
        }

        try
        {
            return await formalBusinessService.DeleteTariffAsync(user, tariffId)
                ? Ok(new { message = "Application price deleted." })
                : NotFound(new { message = "Application price not found." });
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
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpGet("businesses")]
    public async Task<ActionResult<List<CustomerBusinessResponse>>> GetCustomerBusinesses()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to load businesses." });
        }

        try
        {
            return Ok(await formalBusinessService.GetCustomerBusinessesAsync(user));
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
    }

    [HttpGet("businesses/{businessId:int}")]
    public async Task<ActionResult<CustomerBusinessResponse>> GetCustomerBusiness(int businessId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to view a business." });
        }

        try
        {
            var business = await formalBusinessService.GetCustomerBusinessAsync(user, businessId);
            return business is null ? NotFound(new { message = "Business not found." }) : Ok(business);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
    }

    [HttpGet("businesses/{businessId:int}/has-application")]
    public async Task<ActionResult<object>> CustomerBusinessHasApplication(int businessId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to check business applications." });
        }

        try
        {
            return Ok(new { hasApplication = await formalBusinessService.CustomerBusinessHasApplicationAsync(user, businessId) });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [HttpPost("businesses")]
    public async Task<ActionResult<CustomerBusinessResponse>> CreateCustomerBusiness(CustomerBusinessSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to add a business." });
        }

        try
        {
            return Ok(await formalBusinessService.CreateCustomerBusinessAsync(user, request));
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
    }

    [HttpPut("businesses/{businessId:int}")]
    public async Task<ActionResult<CustomerBusinessResponse>> UpdateCustomerBusiness(int businessId, CustomerBusinessSaveRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to update a business." });
        }

        try
        {
            var business = await formalBusinessService.UpdateCustomerBusinessAsync(user, businessId, request);
            return business is null ? NotFound(new { message = "Business not found." }) : Ok(business);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
    }

    [HttpDelete("businesses/{businessId:int}")]
    public async Task<ActionResult> DeactivateCustomerBusiness(int businessId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to remove a business." });
        }

        try
        {
            return await formalBusinessService.DeactivateCustomerBusinessAsync(user, businessId)
                ? Ok(new { message = "Business removed." })
                : NotFound(new { message = "Business not found." });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
    }

    [HttpGet("applications/track/{trackingNumber}")]
    public async Task<ActionResult<TrackingApplicationResponse>> TrackApplication(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber) || !trackingNumber.StartsWith("BCLMS-", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Enter a valid tracking number." });
        }

        var application = await formalBusinessService.TrackApplicationAsync(trackingNumber);
        return application is null
            ? NotFound(new { message = "No application found for that tracking number." })
            : Ok(application);
    }

    [HttpGet("applications/mine")]
    public async Task<ActionResult<List<InternalApplicationSummaryResponse>>> GetMyApplications()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to load your applications." });
        }

        return Ok(await formalBusinessService.GetMyApplicationsAsync(user));
    }

    [HttpGet("applications/internal")]
    public async Task<ActionResult<List<InternalApplicationSummaryResponse>>> GetInternalApplications()
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to load applications." });
        }

        return Ok(await formalBusinessService.GetInternalApplicationsAsync(user));
    }

    [HttpGet("applications/internal/{applicationId:int}")]
    public async Task<ActionResult<InternalApplicationDetailResponse>> GetInternalApplicationDetail(int applicationId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to view application details." });
        }

        var application = await formalBusinessService.GetInternalApplicationDetailAsync(applicationId, user);
        return application is null
            ? NotFound(new { message = "Application not found." })
            : Ok(application);
    }

    [HttpGet("applications/internal/{applicationId:int}/documents/{applicationDocumentId:int}/file")]
    public async Task<ActionResult> GetApplicationDocumentFile(int applicationId, int applicationDocumentId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to view documents." });
        }

        try
        {
            var document = await formalBusinessService.GetApplicationDocumentFileAsync(applicationId, applicationDocumentId, user);
            if (document is null || document.FileContent.Length == 0)
            {
                return NotFound(new { message = "Document not found." });
            }

            return File(
                document.FileContent,
                string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType,
                string.IsNullOrWhiteSpace(document.OriginalFileName) ? document.DocumentName : document.OriginalFileName);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }

    [HttpPost("applications/internal/{applicationId:int}/archive")]
    public async Task<ActionResult> ArchiveApplication(int applicationId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to archive applications." });
        }

        try
        {
            return await formalBusinessService.ArchiveApplicationAsync(applicationId, user)
                ? Ok(new { message = "Application archived." })
                : NotFound(new { message = "Application not found or already archived." });
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
    }

    [HttpPost("applications/{applicationId:int}/cancel")]
    public async Task<ActionResult> CancelCustomerApplication(int applicationId)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to cancel applications." });
        }

        try
        {
            return await formalBusinessService.CancelCustomerApplicationAsync(applicationId, user)
                ? Ok(new { message = "Application cancelled." })
                : BadRequest(new { message = "Only submitted applications can be cancelled." });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }

    [HttpPost("applications/internal/{applicationId:int}/workflow-action")]
    public async Task<ActionResult<InternalApplicationDetailResponse>> ProcessWorkflowStep(int applicationId, WorkflowStepActionRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to process workflow steps." });
        }

        try
        {
            var application = await formalBusinessService.ProcessWorkflowStepAsync(applicationId, request, user);
            return application is null
                ? BadRequest(new { message = "The current workflow step could not be processed." })
                : Ok(application);
        }
        catch (UnauthorizedAccessException error)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = error.Message });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = error.InnerException?.Message ?? error.Message });
        }
    }

    [RequestSizeLimit(MultipartRequestBytes)]
    [HttpPost("applications/{applicationId:int}/attachments")]
    public async Task<ActionResult<AttachmentUploadResponse>> UploadAttachment(int applicationId, [FromForm] AttachmentUploadRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);

        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to upload documents." });
        }

        try
        {
            return Ok(await formalBusinessService.UploadAttachmentAsync(applicationId, request, user));
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new { message = error.Message });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch(Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("draft")]
    public ActionResult<FormalBusinessDraftResponse> SaveDraft(FormalBusinessDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TradeName))
        {
            return BadRequest(new { message = "Trade name is required before saving a formal business draft." });
        }

        return Ok(formalBusinessService.SaveDraft(request));
    }

    [HttpPost("applications")]
    public async Task<ActionResult<ApplicationSubmitResponse>> SubmitApplication(ApplicationSubmitRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to submit an application." });
        }

        if (string.IsNullOrWhiteSpace(request.ApplicantName)
            || string.IsNullOrWhiteSpace(request.IdNumber)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Phone)
            || !request.BusinessId.HasValue
            || string.IsNullOrWhiteSpace(request.LicenceType)
            || !request.TownshipId.HasValue)
        {
            return BadRequest(new { message = "Required application fields are missing." });
        }

        if (!IsValidSouthAfricanIdNumber(request.IdNumber))
        {
            return BadRequest(new { message = "Enter a valid 13-digit South African ID number." });
        }

        try
        {
            return Ok(await formalBusinessService.SubmitApplicationAsync(request, user.UserId));
        }
        catch (DuplicateApplicationException error)
        {
            return Conflict(new { message = error.Message });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The application could not be saved." });
        }
    }

    [HttpPut("applications/{applicationId:int}/resubmit")]
    public async Task<ActionResult<ApplicationSubmitResponse>> ResubmitRejectedApplication(int applicationId, ApplicationSubmitRequest request)
    {
        var user = userTokenService.GetValidTokenPayload(Request.Headers.Authorization);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Login is required to update an application." });
        }

        try
        {
            var application = await formalBusinessService.ResubmitRejectedApplicationAsync(applicationId, request, user.UserId);
            return application is null
                ? BadRequest(new { message = "Only rejected applications can be updated and resubmitted." })
                : Ok(application);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The application could not be updated." });
        }
    }

    private static bool IsValidSouthAfricanIdNumber(string idNumber)
    {
        idNumber = idNumber.Trim();
        if (idNumber.Length != 13 || !idNumber.All(char.IsDigit))
        {
            return false;
        }

        var year = int.Parse(idNumber[..2]);
        var month = int.Parse(idNumber.Substring(2, 2));
        var day = int.Parse(idNumber.Substring(4, 2));
        var currentTwoDigitYear = DateTime.Today.Year % 100;
        var fullYear = year <= currentTwoDigitYear ? 2000 + year : 1900 + year;

        if (!DateTime.TryParse($"{fullYear:D4}-{month:D2}-{day:D2}", out var birthDate)
            || birthDate.Year != fullYear
            || birthDate.Month != month
            || birthDate.Day != day
            || birthDate.Date > DateTime.Today)
        {
            return false;
        }

        var oddPositionTotal = 0;
        var evenPositionDigits = string.Empty;
        for (var index = 0; index < 12; index++)
        {
            if (index % 2 == 0)
            {
                oddPositionTotal += idNumber[index] - '0';
            }
            else
            {
                evenPositionDigits += idNumber[index];
            }
        }

        var doubledEvenDigits = (long.Parse(evenPositionDigits) * 2).ToString();
        var evenPositionTotal = doubledEvenDigits.Sum(digit => digit - '0');
        var calculatedCheckDigit = (10 - ((oddPositionTotal + evenPositionTotal) % 10)) % 10;

        return calculatedCheckDigit == idNumber[12] - '0';
    }
}
