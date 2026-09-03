namespace BCLMSApi.Models;

public class WorkshopInvitationRequest
{
    public List<int> ApplicationIds { get; set; } = [];
    public List<int> BusinessIds { get; set; } = [];
    public List<int> RequestIds { get; set; } = [];
    public string EmailSubject { get; set; } = string.Empty;
    public string EmailBody { get; set; } = string.Empty;
    public string SmsMessage { get; set; } = string.Empty;
    public bool SendEmail { get; set; } = true;
    public bool SendSms { get; set; } = true;
}

public class WorkshopInvitationResponse
{
    public int TotalApplicants { get; set; }
    public int EmailSent { get; set; }
    public int SmsSent { get; set; }
    public List<WorkshopInvitationRecipientResult> Recipients { get; set; } = [];
}

public class WorkshopInvitationRecipientResult
{
    public int ApplicationId { get; set; }
    public int BusinessId { get; set; }
    public int WorkshopAttendanceRequestId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
    public bool SmsSent { get; set; }
    public string? Error { get; set; }
}

public class WorkshopBusinessResponse
{
    public int BusinessId { get; set; }
    public int UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? TownshipName { get; set; }
    public bool WorkshopAttended { get; set; }
    public DateTime? WorkshopAttendedDate { get; set; }
}

public class WorkshopAttendanceRequestCreateRequest
{
    public int BusinessId { get; set; }
    public string RequestedLicenceType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class WorkshopAttendanceRequestResponse
{
    public int WorkshopAttendanceRequestId { get; set; }
    public int BusinessId { get; set; }
    public int UserId { get; set; }
    public string RequestedLicenceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public DateTime? ScheduledWorkshopDate { get; set; }
    public DateTime? InvitedDate { get; set; }
    public DateTime? AttendedDate { get; set; }
    public string? Notes { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? TownshipName { get; set; }
}

public class WorkshopAttendanceScheduleRequest
{
    public DateTime? ScheduledWorkshopDate { get; set; }
}
