namespace BCLMSApi.Models;

public class ComplaintCreateRequest
{
    public string Category { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public class ComplaintStatusUpdateRequest
{
    public string Status { get; set; } = string.Empty;

    public string? OfficialResponse { get; set; }
}

public class ComplaintResponse
{
    public int ComplaintId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? OfficialResponse { get; set; }

    public string CreatedDate { get; set; } = string.Empty;

    public string? UpdatedDate { get; set; }
}
