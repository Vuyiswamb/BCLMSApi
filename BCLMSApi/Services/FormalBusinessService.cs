using System.Security.Cryptography;
using System.Net;
using BCLMSApi.Data;
using BCLMSApi.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BCLMSApi.Services;

public class FormalBusinessService(IFormalBusinessRepository formalBusinessRepository, Datalayer datalayer, IEmailService emailService) : IFormalBusinessService
{
    private const long MaxPdfBytes = 10 * 1024 * 1024;
    private static readonly byte[] PdfHeader = "%PDF"u8.ToArray();

    public FormalBusinessApplicationTemplate GetNewApplicationTemplate()
    {
        return new FormalBusinessApplicationTemplate
        {
            LicenceTypes =
            [
                "Formal Business",
                "Hawkers Licence",
                "Trade Stand Permit",
                "Food Vending Licence",
                "Event Licence"
            ],
            Requirements =
            [
                "Zoning certificate",
                "Approved building plans",
                "ID / passport",
                "CIPC documents",
                "Power of attorney if applying on behalf of someone",
                "Lease agreement",
                "Title deed document",
                "Proof of residence",
                "SARS documents",
                "Affidavit if Res 5",
                "Menu for cafe keeper",
                "Health report",
                "Fire report",
                "Liquor licence",
                "Gambling authority",
                "Proof of payment"
            ],
            DiaryDepartments =
            [
                "City Planning",
                "Health Department",
                "Fire Department"
            ]
        };
    }

    public Task<List<AttachmentTypeResponse>> GetAttachmentTypesAsync()
    {
        return formalBusinessRepository.GetAttachmentTypesAsync();
    }

    public Task<List<TownshipResponse>> GetTownshipsAsync()
    {
        return formalBusinessRepository.GetTownshipsAsync();
    }

    public Task<List<CustomerBusinessResponse>> GetCustomerBusinessesAsync(UserTokenPayload user)
    {
        return formalBusinessRepository.GetCustomerBusinessesAsync(user.UserId);
    }

    public Task<CustomerBusinessResponse> CreateCustomerBusinessAsync(UserTokenPayload user, CustomerBusinessSaveRequest request)
    {
        ValidateCustomerBusiness(request);
        ValidateCustomerBusinessCipcDocument(request);
        return formalBusinessRepository.CreateCustomerBusinessAsync(user.UserId, request);
    }

    public Task<CustomerBusinessResponse?> UpdateCustomerBusinessAsync(UserTokenPayload user, int businessId, CustomerBusinessSaveRequest request)
    {
        ValidateBusinessId(businessId);
        ValidateCustomerBusiness(request);
        ValidateCustomerBusinessCipcDocument(request);
        return formalBusinessRepository.UpdateCustomerBusinessAsync(user.UserId, businessId, request);
    }

    public Task<CustomerBusinessResponse?> GetCustomerBusinessAsync(UserTokenPayload user, int businessId)
    {
        ValidateBusinessId(businessId);
        return formalBusinessRepository.GetCustomerBusinessAsync(user.UserId, businessId);
    }

    public Task<bool> CustomerBusinessHasApplicationAsync(UserTokenPayload user, int businessId)
    {
        ValidateBusinessId(businessId);
        return formalBusinessRepository.BusinessHasApplicationAsync(user.UserId, businessId);
    }

    public Task<bool> DeactivateCustomerBusinessAsync(UserTokenPayload user, int businessId)
    {
        ValidateBusinessId(businessId);
        return formalBusinessRepository.DeactivateCustomerBusinessAsync(user.UserId, businessId);
    }

    public async Task<TrackingApplicationResponse?> TrackApplicationAsync(string trackingNumber)
    {
        var application = await formalBusinessRepository.TrackApplicationAsync(trackingNumber);
        if (application is not null && application.Steps.Count == 0)
        {
            application.Steps = CreateDefaultTrackingSteps(application.LicenceType, application.CurrentStage, application.Status);
        }
        else if (application is not null)
        {
            application.Steps = EnsureTrackingWorkshopStep(application.LicenceType, application.Steps);
        }

        return application;
    }

    public Task<List<InternalApplicationSummaryResponse>> GetInternalApplicationsAsync(UserTokenPayload user)
    {
        return formalBusinessRepository.GetInternalApplicationsAsync(
            user.IsCustomer ? user.UserId : null,
            HasAllRegionAccess(user),
            user.Regions);
    }

    public Task<List<InternalApplicationSummaryResponse>> GetMyApplicationsAsync(UserTokenPayload user)
    {
        return formalBusinessRepository.GetInternalApplicationsAsync(user.UserId);
    }

    public async Task<InternalApplicationDetailResponse?> GetInternalApplicationDetailAsync(int applicationId, UserTokenPayload user)
    {
        var application = await formalBusinessRepository.GetInternalApplicationDetailAsync(
            applicationId,
            user.IsCustomer ? user.UserId : null,
            HasAllRegionAccess(user),
            user.Regions);
        if (application is not null && application.WorkflowSteps.Count == 0)
        {
            application.WorkflowSteps = CreateDefaultInternalWorkflowSteps(application);
        }
        else if (application is not null)
        {
            application.WorkflowSteps = EnsureInternalWorkshopStep(application);
        }

        return application;
    }

    public Task<ApplicationDocumentFileResponse?> GetApplicationDocumentFileAsync(int applicationId, int applicationDocumentId, UserTokenPayload user)
    {
        if (applicationId <= 0 || applicationDocumentId <= 0)
        {
            throw new ArgumentException("Select a valid document.");
        }

        return formalBusinessRepository.GetApplicationDocumentFileAsync(applicationId, applicationDocumentId, user.IsCustomer ? user.UserId : null);
    }

    public Task<bool> ArchiveApplicationAsync(int applicationId, UserTokenPayload user)
    {
        if (!user.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only Super Users can archive applications.");
        }

        return formalBusinessRepository.ArchiveApplicationAsync(applicationId, user.UserId);
    }

    public Task<bool> CancelCustomerApplicationAsync(int applicationId, UserTokenPayload user)
    {
        if (applicationId <= 0)
        {
            throw new ArgumentException("Select a valid application.");
        }

        return formalBusinessRepository.CancelCustomerApplicationAsync(applicationId, user.UserId);
    }

    public async Task<AttachmentUploadResponse> UploadAttachmentAsync(int applicationId, AttachmentUploadRequest request, UserTokenPayload user)
    {
        if (request.File is null || request.File.Length == 0)
        {
            throw new ArgumentException("A PDF file is required.");
        }

        var validationError = await ValidatePdfAsync(request.File);
        if (validationError is not null)
        {
            throw new ArgumentException(validationError);
        }

        if (!await formalBusinessRepository.ApplicationExistsAsync(applicationId, user.IsCustomer ? user.UserId : null))
        {
            throw new KeyNotFoundException("Application not found.");
        }

        var attachmentType = await formalBusinessRepository.GetAttachmentTypeAsync(request.AttachmentTypeId);
        if (attachmentType is null)
        {
            throw new ArgumentException("Invalid attachment type.");
        }

        await using var stream = request.File.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var fileBytes = memory.ToArray();
        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        var safeFileName = Path.GetFileName(request.File.FileName);
        var storedFileName = $"{Guid.NewGuid():N}.pdf";
        var documentId = await formalBusinessRepository.InsertPdfDocumentAsync(
            applicationId,
            attachmentType,
            safeFileName,
            storedFileName,
            fileBytes.LongLength,
            fileBytes,
            fileHash,
            request.Remarks);

        return new AttachmentUploadResponse
        {
            ApplicationDocumentId = documentId,
            AttachmentTypeId = request.AttachmentTypeId,
            FileName = safeFileName,
            FileSizeBytes = fileBytes.LongLength,
            Sha256Hash = fileHash,
            Status = "Submitted"
        };
    }

    public FormalBusinessDraftResponse SaveDraft(FormalBusinessDraftRequest request)
    {
        var referenceNumber = string.IsNullOrWhiteSpace(request.ApplicationNumber)
            ? $"FB-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.ApplicationNumber;

        return new FormalBusinessDraftResponse
        {
            ApplicationNumber = referenceNumber,
            Status = "Draft",
            SavedAt = DateTime.UtcNow
        };
    }

    public async Task<ApplicationSubmitResponse> SubmitApplicationAsync(ApplicationSubmitRequest request, int userId)
    {
        try
        {
            if (!request.BusinessId.HasValue)
            {
                throw new ArgumentException("Select a business for this application.");
            }

            var business = await formalBusinessRepository.GetCustomerBusinessAsync(userId, request.BusinessId.Value)
                ?? throw new ArgumentException("Select a valid business from your account.");
            request.BusinessName = business.BusinessName;
            request.RegistrationNumber = business.RegistrationNumber;
            request.TownshipId = business.TownshipId ?? request.TownshipId;
            request.WardNumber = business.WardNumber ?? request.WardNumber;
            request.Address = business.PhysicalAddress;

            if (!request.ApplicationFee.HasValue || request.ApplicationFee.Value < 0)
            {
                throw new ArgumentException("Enter a valid application fee.");
            }

            var licenceType = await ResolveLicenceTypeAsync(request.LicenceType, userId, request.BusinessId.Value);
            var workshopAttended = business.WorkshopAttended || await HasAttendedWorkshopRequestAsync(userId, request.BusinessId.Value);
            if (!licenceType.StartsWith("Formal Business", StringComparison.OrdinalIgnoreCase)
                && !workshopAttended)
            {
                throw new ArgumentException("A workshop must be attended for this Company/Business Owner before submitting this application type.");
            }

            if (licenceType.Contains("Hawkers", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(request.PostalAddress))
            {
                throw new ArgumentException("Postal address is required for Hawkers Licence applications.");
            }

            ValidateTradingBusinessType(licenceType, request.TradeStandBusinessType);

            return await formalBusinessRepository.SubmitApplicationAsync(request, licenceType, userId);

        }
        catch(Exception)
        {
            throw;
        }


    }

    private async Task<bool> HasAttendedWorkshopRequestAsync(int userId, int businessId)
    {
        await using var command = datalayer.CreateTextCommand("""
            SELECT CONVERT(BIT, CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.WorkshopAttendanceRequests
                WHERE UserId = @UserId
                  AND BusinessId = @BusinessId
                  AND IsActive = 1
                  AND Status = 'Attended'
            )
            THEN 1 ELSE 0 END);
            """);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@BusinessId", businessId);
        await command.Connection!.OpenAsync();
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    public async Task<ApplicationSubmitResponse?> ResubmitRejectedApplicationAsync(int applicationId, ApplicationSubmitRequest request, int userId)
    {
        if (applicationId <= 0)
        {
            throw new ArgumentException("Select a valid application.");
        }

        var existingApplication = await formalBusinessRepository.GetInternalApplicationDetailAsync(applicationId, userId)
            ?? throw new ArgumentException("Application not found.");

        if (!existingApplication.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only rejected applications can be updated and resubmitted.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new ArgumentException("Email address and mobile number are required.");
        }

        request.Address = string.IsNullOrWhiteSpace(request.Address)
            ? existingApplication.PhysicalAddress
            : request.Address;

        var licenceType = existingApplication.LicenceType;
        if (licenceType.Contains("Hawkers", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.PostalAddress))
        {
            throw new ArgumentException("Postal address is required for Hawkers Licence applications.");
        }

        ValidateTradingBusinessType(licenceType, request.TradeStandBusinessType);

        return await formalBusinessRepository.ResubmitRejectedApplicationAsync(applicationId, request, licenceType, userId);
    }

    public async Task<InternalApplicationDetailResponse?> ProcessWorkflowStepAsync(int applicationId, WorkflowStepActionRequest request, UserTokenPayload user)
    {
        if (user.IsCustomer)
        {
            throw new UnauthorizedAccessException("Only municipal officials can process workflow steps.");
        }

        if (applicationId <= 0)
        {
            throw new ArgumentException("Select a valid application.");
        }

        var decision = request.Decision.Trim();
        if (!decision.Equals("Approve", StringComparison.OrdinalIgnoreCase)
            && !decision.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            && !decision.Equals("Complete", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Select Approve, Reject, or Complete.");
        }

        if (decision.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new ArgumentException("A rejection reason is required.");
        }

        if (decision.Equals("Approve", StringComparison.OrdinalIgnoreCase)
            && (request.DocumentName?.Contains("Admin Approval", StringComparison.OrdinalIgnoreCase) == true
                || request.DocumentName?.Contains("Director Approval", StringComparison.OrdinalIgnoreCase) == true)
            && string.IsNullOrWhiteSpace(request.SignatureBase64))
        {
            throw new ArgumentException("This approval step requires a signature.");
        }

        if (!string.IsNullOrWhiteSpace(request.FileBase64))
        {
            var fileBytes = Convert.FromBase64String(NormalizeBase64(request.FileBase64));
            if (fileBytes.LongLength > MaxPdfBytes)
            {
                throw new ArgumentException("The PDF file may not exceed 10 MB.");
            }

            if (fileBytes.Length < PdfHeader.Length || !fileBytes.AsSpan(0, PdfHeader.Length).SequenceEqual(PdfHeader))
            {
                throw new ArgumentException("The uploaded file is not a valid PDF document.");
            }

            if (!string.Equals(Path.GetExtension(request.FileName ?? string.Empty), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only PDF files are allowed.");
            }

            if (!string.Equals(request.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The uploaded file content type must be application/pdf.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SignatureBase64))
        {
            if (!request.SignatureBase64.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The signature must be captured as a PNG image.");
            }

            var signatureBytes = Convert.FromBase64String(NormalizeBase64(request.SignatureBase64));
            if (signatureBytes.LongLength > MaxPdfBytes)
            {
                throw new ArgumentException("The signature image may not exceed 10 MB.");
            }
        }

        var updatedApplication = await formalBusinessRepository.ProcessWorkflowStepAsync(applicationId, request, user);
        if (updatedApplication is not null)
        {
            await SendWorkflowStepChangedEmailAsync(updatedApplication, request, user);
        }

        return updatedApplication;
    }

    private async Task SendWorkflowStepChangedEmailAsync(InternalApplicationDetailResponse application, WorkflowStepActionRequest request, UserTokenPayload user)
    {
        if (string.IsNullOrWhiteSpace(application.EmailAddress))
        {
            return;
        }

        try
        {
            await emailService.SendEmailOffice365Async(
                application.EmailAddress,
                $"BCLMS application {application.TrackingNumber} workflow update",
                BuildWorkflowStepChangedEmailBody(application, request, user));
        }
        catch
        {
            // The workflow update has already been saved; email failure must not roll it back.
        }
    }

    private static string BuildWorkflowStepChangedEmailBody(InternalApplicationDetailResponse application, WorkflowStepActionRequest request, UserTokenPayload user)
    {
        var applicantName = WebUtility.HtmlEncode(application.ApplicantName);
        var trackingNumber = WebUtility.HtmlEncode(application.TrackingNumber);
        var businessName = WebUtility.HtmlEncode(application.BusinessName);
        var licenceType = WebUtility.HtmlEncode(application.LicenceType);
        var decision = WebUtility.HtmlEncode(request.Decision.Trim());
        var currentStage = WebUtility.HtmlEncode(application.CurrentStage);
        var officialName = WebUtility.HtmlEncode(user.DisplayName);
        var activeStep = application.WorkflowSteps
            .OrderBy(step => step.SequenceNumber)
            .FirstOrDefault(step => !IsCompletedWorkflowStatus(step.Status));
        var nextAction = activeStep is null
            ? "The application workflow is complete."
            : $"The application is now waiting for <strong>{WebUtility.HtmlEncode(activeStep.StepName)}</strong>.";
        var rejectionReason = string.IsNullOrWhiteSpace(request.RejectionReason)
            ? string.Empty
            : $@"
            <div style='background:#fff2f2; border-left:4px solid #d93025; padding:16px 18px; margin:24px 0;'>
                <p style='margin:0 0 8px; font-weight:bold; color:#8a1616;'>Reason / comments</p>
                <p style='margin:0;'>{WebUtility.HtmlEncode(request.RejectionReason.Trim())}</p>
            </div>";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Application Workflow Update</title>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; background:#edf2f5; margin:0; padding:24px;'>
    <div style='display:none; max-height:0; overflow:hidden; opacity:0;'>Your BCLMS application workflow has been updated.</div>
    <div style='max-width:680px; margin:0 auto; background:#ffffff; border-radius:8px; overflow:hidden; border:1px solid #d8e2e8;'>
        <div style='background:#ffffff; padding:24px 32px; border-bottom:4px solid #16821f;'>
            <p style='margin:0; color:#16821f; font-weight:bold; letter-spacing:.04em; text-transform:uppercase; font-size:12px;'>Business Compliance & Licensing Management System</p>
            <h1 style='margin:10px 0 0; font-size:24px; line-height:1.25; color:#102033;'>Application workflow update</h1>
        </div>
        <div style='padding:32px; color:#102033; line-height:1.65;'>
            <p style='margin:0 0 18px;'>Dear {applicantName},</p>
            <p style='margin:0 0 18px;'>There has been an update on your City of Tshwane business licence application.</p>
            <table role='presentation' style='width:100%; border-collapse:collapse; margin:24px 0;'>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Reference number</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; text-align:right;'>{trackingNumber}</td>
                </tr>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Business</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; text-align:right;'>{businessName}</td>
                </tr>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Application type</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; text-align:right;'>{licenceType}</td>
                </tr>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Latest action</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; color:#16821f; text-align:right;'>{decision}</td>
                </tr>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Current stage</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; text-align:right;'>{currentStage}</td>
                </tr>
            </table>
            <div style='background:#f3f8f4; border-left:4px solid #16821f; padding:16px 18px; margin:24px 0;'>
                <p style='margin:0 0 8px; font-weight:bold; color:#0f2f25;'>What happens next</p>
                <p style='margin:0;'>{nextAction}</p>
            </div>
            {rejectionReason}
            <p style='margin:0 0 18px;'>Processed by: <strong>{officialName}</strong></p>
            <p style='margin:0;'>Regards,<br />City of Tshwane<br />Business Compliance & Licensing</p>
        </div>
        <div style='background:#102033; color:#dce7ed; padding:20px 32px; font-size:12px; line-height:1.5;'>
            <p style='margin:0 0 8px;'>This is an automated notification from BCLMS. Please do not reply to this email.</p>
            <p style='margin:0;'>City of Tshwane Metropolitan Municipality</p>
        </div>
    </div>
</body>
</html>";
    }

    private static bool IsCompletedWorkflowStatus(string status)
    {
        return status.Trim().Equals("Approved", StringComparison.OrdinalIgnoreCase)
            || status.Trim().Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Trim().Equals("Complete", StringComparison.OrdinalIgnoreCase);
    }

    public Task<TariffResponse?> GetTariffAsync(string licenceType, string applicationKind)
    {
        if (string.IsNullOrWhiteSpace(licenceType))
        {
            throw new ArgumentException("Select an application type.");
        }

        var normalizedLicenceType = licenceType.Trim();
        if (normalizedLicenceType.StartsWith("Formal Business", StringComparison.OrdinalIgnoreCase))
        {
            normalizedLicenceType = "Formal Business";
        }

        var normalizedApplicationKind = string.IsNullOrWhiteSpace(applicationKind) ? "New" : applicationKind.Trim();
        if (normalizedApplicationKind.Equals("New Application", StringComparison.OrdinalIgnoreCase))
        {
            normalizedApplicationKind = "New";
        }

        return formalBusinessRepository.GetTariffAsync(normalizedLicenceType, normalizedApplicationKind);
    }

    public Task<List<TariffResponse>> GetTariffsAsync(UserTokenPayload user)
    {
        RequireSuperUser(user);
        return formalBusinessRepository.GetTariffsAsync();
    }

    public Task<TariffResponse> SaveTariffAsync(UserTokenPayload user, TariffSaveRequest request)
    {
        RequireSuperUser(user);
        NormalizeTariffRequest(request);
        return formalBusinessRepository.SaveTariffAsync(request);
    }

    public Task<TariffResponse?> UpdateTariffAsync(UserTokenPayload user, int tariffId, TariffSaveRequest request)
    {
        RequireSuperUser(user);
        if (tariffId <= 0)
        {
            throw new ArgumentException("Select an application price to update.");
        }

        NormalizeTariffRequest(request);
        return formalBusinessRepository.UpdateTariffAsync(tariffId, request);
    }

    public Task<bool> DisableTariffAsync(UserTokenPayload user, int tariffId)
    {
        RequireSuperUser(user);
        if (tariffId <= 0)
        {
            throw new ArgumentException("Select an application price to disable.");
        }

        return formalBusinessRepository.DisableTariffAsync(tariffId);
    }

    public Task<bool> DeleteTariffAsync(UserTokenPayload user, int tariffId)
    {
        RequireSuperUser(user);
        if (tariffId <= 0)
        {
            throw new ArgumentException("Select an application price to delete.");
        }

        return formalBusinessRepository.DeleteTariffAsync(tariffId);
    }

    private static void RequireSuperUser(UserTokenPayload user)
    {
        if (!user.Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only Super Users can manage application prices.");
        }
    }

    private static bool HasAllRegionAccess(UserTokenPayload user)
    {
        return user.IsCustomer || user.IsSuperUser || user.HasAllRegions;
    }

    private static void NormalizeTariffRequest(TariffSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LicenceType))
        {
            throw new ArgumentException("Select an application type.");
        }

        if (request.FeeAmount < 0)
        {
            throw new ArgumentException("Cost / Price cannot be negative.");
        }

        request.LicenceType = request.LicenceType.Trim();
        request.ApplicationKind = string.IsNullOrWhiteSpace(request.ApplicationKind) ? "New" : request.ApplicationKind.Trim();
        if (request.ApplicationKind.Equals("New Application", StringComparison.OrdinalIgnoreCase))
        {
            request.ApplicationKind = "New";
        }
    }

    private async Task<string> ResolveLicenceTypeAsync(string licenceType, int userId, int businessId)
    {
        if (!string.Equals(licenceType.Trim(), "Formal Business", StringComparison.OrdinalIgnoreCase))
        {
            return licenceType.Trim();
        }

        return await formalBusinessRepository.BusinessHasApplicationAsync(userId, businessId)
            ? "Formal Business - Renewal"
            : "Formal Business - New Application";
    }

    private async Task<string?> ValidatePdfAsync(IFormFile file)
    {
        if (file.Length > MaxPdfBytes)
        {
            return "The PDF file may not exceed 10 MB.";
        }

        var fileName = Path.GetFileName(file.FileName);
        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "Only PDF files are allowed.";
        }

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "The uploaded file content type must be application/pdf.";
        }

        await using var stream = file.OpenReadStream();
        var header = new byte[PdfHeader.Length];
        var bytesRead = await stream.ReadAsync(header);
        if (bytesRead != PdfHeader.Length || !header.SequenceEqual(PdfHeader))
        {
            return "The uploaded file is not a valid PDF document.";
        }

        return null;
    }

    private static string NormalizeBase64(string base64Value)
    {
        var value = base64Value.Trim();
        var commaIndex = value.IndexOf(',');
        return commaIndex >= 0 ? value[(commaIndex + 1)..] : value;
    }

    private static void ValidateBusinessId(int businessId)
    {
        if (businessId <= 0)
        {
            throw new ArgumentException("Select a valid business.");
        }
    }

    private static void ValidateCustomerBusiness(CustomerBusinessSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName)
            || string.IsNullOrWhiteSpace(request.PhysicalAddress)
            || !request.TownshipId.HasValue)
        {
            throw new ArgumentException("Business name, area/suburb, and physical address are required.");
        }

        if (request.BusinessName.Trim().Length > 200)
        {
            throw new ArgumentException("Business name may not exceed 200 characters.");
        }
    }

    private static void ValidateCustomerBusinessCipcDocument(CustomerBusinessSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CipcDocumentBase64))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.CipcDocumentFileName))
        {
            throw new ArgumentException("The CIPC document file name is required.");
        }

        if (!string.Equals(Path.GetExtension(request.CipcDocumentFileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only PDF files are allowed for the CIPC document.");
        }

        byte[] fileBytes;
        try
        {
            var base64Value = request.CipcDocumentBase64.Trim();
            var commaIndex = base64Value.IndexOf(',');
            if (commaIndex >= 0)
            {
                base64Value = base64Value[(commaIndex + 1)..];
            }

            fileBytes = Convert.FromBase64String(base64Value);
        }
        catch (FormatException)
        {
            throw new ArgumentException("The CIPC document is not valid base64 content.");
        }

        if (fileBytes.LongLength > MaxPdfBytes)
        {
            throw new ArgumentException("The CIPC PDF document may not exceed 10 MB.");
        }

        if (fileBytes.Length < PdfHeader.Length || !fileBytes.Take(PdfHeader.Length).SequenceEqual(PdfHeader))
        {
            throw new ArgumentException("The CIPC document must be a valid PDF file.");
        }
    }

    private static List<TrackingWorkflowStepResponse> CreateDefaultTrackingSteps(string licenceType, string currentStage, string applicationStatus)
    {
        var steps = new List<TrackingWorkflowStepResponse>
        {
            new() { Name = "Application intake", Group = "Compliance Officer", Status = "Pending" },
            new() { Name = "Zoning verification", Group = "City Planning", Status = "Pending" },
            new() { Name = "Health report", Group = "Health Department", Status = "Pending" },
            new() { Name = "Fire report", Group = "Fire Department", Status = "Pending" },
            new() { Name = "Senior specialist review", Group = "Senior Specialist", Status = "Pending" },
            new() { Name = "Final licence decision", Group = "Compliance Officer", Status = "Pending" }
        };

        if (IsFormalLicence(licenceType))
        {
            steps.Insert(1, new() { Name = "Proof of payment verification", Group = "Compliance Officer", Status = "Pending" });
            steps.Insert(2, new() { Name = "CIPC verification", Group = "Compliance Officer", Status = "Pending" });
        }

        var currentIndex = steps.FindIndex(step => string.Equals(step.Name, currentStage, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        for (var index = 0; index < steps.Count; index += 1)
        {
            if (index < currentIndex)
            {
                steps[index].Status = "Approved";
            }
            else if (index == currentIndex)
            {
                steps[index].Status = applicationStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase)
                    ? "In progress"
                    : applicationStatus;
            }
        }

        return EnsureTrackingWorkshopStep(licenceType, steps);
    }

    private static List<InternalApplicationWorkflowStepResponse> CreateDefaultInternalWorkflowSteps(InternalApplicationDetailResponse application)
    {
        var isHawkers = string.Equals(application.LicenceType, "Hawkers Licence", StringComparison.OrdinalIgnoreCase);
        var isFormal = IsFormalLicence(application.LicenceType);
        var definitions = isHawkers
            ? new (string StepName, string GroupName)[]
            {
                ("Workshop", "Compliance Officer"),
                ("Application Submitted", "Compliance Officer"),
                ("Home Affairs Verification", "Compliance Officer"),
                ("TMPD Inspection (Site Inspection)", "Metro Police"),
                ("Admin Approval", "Compliance Officer"),
                ("Functional Head Approval", "Functional Head"),
                ("Director Approval", "Director"),
                ("Licence Issued", "Compliance Officer")
            }
            : !isFormal
                ? new (string StepName, string GroupName)[]
                {
                    ("Workshop", "Compliance Officer"),
                    ("Application intake", "Compliance Officer"),
                    ("Zoning verification", "City Planning"),
                    ("Health report", "Health Department"),
                    ("Fire report", "Fire Department"),
                    ("Senior specialist review", "Senior Specialist"),
                    ("Final licence decision", "Compliance Officer")
                }
            : new (string StepName, string GroupName)[]
            {
                ("Application intake", "Compliance Officer"),
                ("Proof of payment verification", "Compliance Officer"),
                ("CIPC verification", "Compliance Officer"),
                ("Zoning verification", "City Planning"),
                ("Health report", "Health Department"),
                ("Fire report", "Fire Department"),
                ("Senior specialist review", "Senior Specialist"),
                ("Final licence decision", "Compliance Officer")
            };

        var currentIndex = Array.FindIndex(definitions, step => string.Equals(step.StepName, application.CurrentStage, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            currentIndex = isFormal ? 0 : 1;
        }

        var steps = new List<InternalApplicationWorkflowStepResponse>();
        for (var index = 0; index < definitions.Length; index += 1)
        {
            var status = "Pending";
            if (index < currentIndex)
            {
                status = "Approved";
            }
            else if (!IsFormalLicence(application.LicenceType) && string.Equals(definitions[index].StepName, "Workshop", StringComparison.OrdinalIgnoreCase))
            {
                status = "Approved";
            }
            else if (index == currentIndex)
            {
                status = application.Status.Equals("Submitted", StringComparison.OrdinalIgnoreCase)
                    ? "In progress"
                    : application.Status;
            }

            steps.Add(new InternalApplicationWorkflowStepResponse
            {
                StepName = definitions[index].StepName,
                GroupName = definitions[index].GroupName,
                SequenceNumber = index + 1,
                Status = status,
                StartedDate = !string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
                    ? application.SubmittedDate
                    : null,
                CompletedDate = string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
                    ? application.SubmittedDate
                    : null
            });
        }

        return steps;
    }

    private static List<InternalApplicationWorkflowStepResponse> EnsureInternalWorkshopStep(InternalApplicationDetailResponse application)
    {
        if (IsFormalLicence(application.LicenceType)
            || application.WorkflowSteps.Any(step => string.Equals(step.StepName, "Workshop", StringComparison.OrdinalIgnoreCase)))
        {
            return application.WorkflowSteps;
        }

        var steps = application.WorkflowSteps
            .OrderBy(step => step.SequenceNumber)
            .Select(step =>
            {
                step.SequenceNumber += 1;
                return step;
            })
            .ToList();

        steps.Insert(0, new InternalApplicationWorkflowStepResponse
        {
            StepName = "Workshop",
            GroupName = "Compliance Officer",
            SequenceNumber = 1,
            Status = "Approved",
            StartedDate = application.SubmittedDate,
            CompletedDate = application.SubmittedDate
        });

        return steps;
    }

    private static List<TrackingWorkflowStepResponse> EnsureTrackingWorkshopStep(string licenceType, List<TrackingWorkflowStepResponse> steps)
    {
        if (IsFormalLicence(licenceType)
            || steps.Any(step => string.Equals(step.Name, "Workshop", StringComparison.OrdinalIgnoreCase)))
        {
            return steps;
        }

        return
        [
            new() { Name = "Workshop", Group = "Compliance Officer", Status = "Approved" },
            .. steps
        ];
    }

    private static bool IsFormalLicence(string licenceType)
    {
        return licenceType.StartsWith("Formal Business", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateTradingBusinessType(string licenceType, string? businessType)
    {
        if (!RequiresTradingBusinessType(licenceType))
        {
            return;
        }

        var allowedBusinessTypes = new[]
        {
            "None -perishable Goods",
            "Perishable Goods",
            "Cell phone Accessories & Airtime Business"
        };

        if (string.IsNullOrWhiteSpace(businessType)
            || !allowedBusinessTypes.Contains(businessType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Select a valid business type for this application.");
        }
    }

    private static bool RequiresTradingBusinessType(string licenceType)
    {
        return licenceType.Contains("Trade Stand", StringComparison.OrdinalIgnoreCase)
            || licenceType.Contains("Hawkers", StringComparison.OrdinalIgnoreCase)
            || licenceType.Contains("Food Vending", StringComparison.OrdinalIgnoreCase);
    }
}
