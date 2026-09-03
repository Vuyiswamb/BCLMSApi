namespace BCLMSApi.Models;

public class CustomerBusinessResponse
{
    public int BusinessId { get; set; }
    public int UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public int? TownshipId { get; set; }
    public string? TownshipName { get; set; }
    public string? WardNumber { get; set; }
    public string PhysicalAddress { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CipcDocumentFileName { get; set; }
    public bool HasCipcDocument { get; set; }
    public bool WorkshopAttended { get; set; }
    public DateTime? WorkshopAttendedDate { get; set; }
    public string? WorkshopRequestStatus { get; set; }
    public DateTime? WorkshopRequestedDate { get; set; }
    public DateTime? WorkshopScheduledDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CustomerBusinessSaveRequest
{
    public string BusinessName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public int? TownshipId { get; set; }
    public string? WardNumber { get; set; }
    public string PhysicalAddress { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CipcDocumentFileName { get; set; }
    public string? CipcDocumentContentType { get; set; }
    public string? CipcDocumentBase64 { get; set; }
}
