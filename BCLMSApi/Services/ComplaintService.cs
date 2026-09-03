using BCLMSApi.Data;
using BCLMSApi.Models;
using System.Net;

namespace BCLMSApi.Services;

public class ComplaintService(IComplaintRepository complaintRepository, IEmailService emailService) : IComplaintService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Submitted",
        "In progress",
        "Awaiting customer",
        "Resolved",
        "Closed",
        "Cancelled"
    };

    public async Task<ComplaintResponse> CreateComplaintAsync(UserTokenPayload user, ComplaintCreateRequest request)
    {
        ValidateComplaint(request);
        return await complaintRepository.CreateComplaintAsync(user.UserId, request);
    }

    public async Task<List<ComplaintResponse>> GetCustomerComplaintsAsync(UserTokenPayload user)
    {
        return await complaintRepository.GetCustomerComplaintsAsync(user.UserId);
    }

    public async Task<List<ComplaintResponse>> GetInternalComplaintsAsync(UserTokenPayload user)
    {
        if (user.IsCustomer)
        {
            throw new UnauthorizedAccessException("Internal access is required to view complaints.");
        }

        return await complaintRepository.GetInternalComplaintsAsync();
    }

    public async Task<ComplaintResponse?> UpdateComplaintStatusAsync(UserTokenPayload user, int complaintId, ComplaintStatusUpdateRequest request)
    {
        if (user.IsCustomer)
        {
            throw new UnauthorizedAccessException("Internal access is required to update complaints.");
        }

        if (complaintId <= 0)
        {
            throw new ArgumentException("Select a valid complaint.");
        }

        if (string.IsNullOrWhiteSpace(request.Status) || !AllowedStatuses.Contains(request.Status.Trim()))
        {
            throw new ArgumentException("Select a valid complaint status.");
        }

        var complaint = await complaintRepository.UpdateComplaintStatusAsync(complaintId, user.UserId, request);
        if (complaint is not null)
        {
            await SendStatusChangedEmailAsync(complaint);
        }

        return complaint;
    }

    public async Task<ComplaintResponse?> CancelComplaintAsync(UserTokenPayload user, int complaintId)
    {
        if (complaintId <= 0)
        {
            throw new ArgumentException("Select a valid complaint.");
        }

        var complaint = await complaintRepository.CancelComplaintAsync(complaintId, user.UserId);
        if (complaint is not null)
        {
            await SendStatusChangedEmailAsync(complaint);
        }

        return complaint;
    }

    private static void ValidateComplaint(ComplaintCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.Subject)
            || string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Category, subject, and description are required.");
        }

        if (request.Subject.Trim().Length > 180)
        {
            throw new ArgumentException("Subject may not exceed 180 characters.");
        }

        if (request.Description.Trim().Length < 20)
        {
            throw new ArgumentException("Provide a complaint description of at least 20 characters.");
        }
    }

    private async Task SendStatusChangedEmailAsync(ComplaintResponse complaint)
    {
        if (string.IsNullOrWhiteSpace(complaint.CustomerEmail))
        {
            return;
        }

        try
        {
            await emailService.SendEmailOffice365Async(
                complaint.CustomerEmail,
                $"BCLMS complaint {complaint.ReferenceNumber} status update",
                BuildStatusChangedEmailBody(complaint));
        }
        catch
        {
            // The complaint status update has already been saved; email failure must not roll it back.
        }
    }

    private static string BuildStatusChangedEmailBody(ComplaintResponse complaint)
    {
        var customerName = WebUtility.HtmlEncode(complaint.CustomerName);
        var referenceNumber = WebUtility.HtmlEncode(complaint.ReferenceNumber);
        var status = WebUtility.HtmlEncode(complaint.Status);
        var subject = WebUtility.HtmlEncode(complaint.Subject);
        var category = WebUtility.HtmlEncode(complaint.Category);
        var officialResponse = string.IsNullOrWhiteSpace(complaint.OfficialResponse)
            ? string.Empty
            : $@"
            <div style='background:#f3f8f4; border-left:4px solid #16821f; padding:16px 18px; margin:24px 0;'>
                <p style='margin:0 0 8px; font-weight:bold; color:#0f2f25;'>Official response</p>
                <p style='margin:0;'>{WebUtility.HtmlEncode(complaint.OfficialResponse)}</p>
            </div>";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Complaint Status Update</title>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; background:#edf2f5; margin:0; padding:24px;'>
    <div style='display:none; max-height:0; overflow:hidden; opacity:0;'>Your BCLMS complaint status has been updated to {status}.</div>
    <div style='max-width:680px; margin:0 auto; background:#ffffff; border-radius:8px; overflow:hidden; border:1px solid #d8e2e8;'>
        <div style='background:#ffffff; padding:24px 32px; border-bottom:4px solid #16821f;'>
            <p style='margin:0; color:#16821f; font-weight:bold; letter-spacing:.04em; text-transform:uppercase; font-size:12px;'>Business Compliance & Licensing Management System</p>
            <h1 style='margin:10px 0 0; font-size:24px; line-height:1.25; color:#102033;'>Complaint status update</h1>
        </div>
        <div style='padding:32px; color:#102033; line-height:1.65;'>
            <p style='margin:0 0 18px;'>Dear {customerName},</p>
            <p style='margin:0 0 18px;'>The status of your complaint has been updated by the City of Tshwane Business Compliance & Licensing team.</p>
            <table role='presentation' style='width:100%; border-collapse:collapse; margin:24px 0;'>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Reference number</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; text-align:right;'>{referenceNumber}</td>
                </tr>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Status</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; color:#16821f; text-align:right;'>{status}</td>
                </tr>
                <tr>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; color:#52657a;'>Category</td>
                    <td style='padding:12px 0; border-bottom:1px solid #dfe6ec; font-weight:bold; text-align:right;'>{category}</td>
                </tr>
            </table>
            <p style='margin:0 0 8px; font-weight:bold; color:#0f2f25;'>Complaint subject</p>
            <p style='margin:0 0 18px;'>{subject}</p>
            {officialResponse}
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
}
