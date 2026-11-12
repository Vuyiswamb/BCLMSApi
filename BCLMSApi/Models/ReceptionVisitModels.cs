namespace BCLMSApi.Models;

public sealed class ReceptionVisitResponse
{
    public int ReceptionVisitId { get; set; }
    public DateTime VisitDate { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string ServiceRequired { get; set; } = string.Empty;
    public string? Referral { get; set; }
    public string CapturedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public sealed class ReceptionVisitCreateRequest
{
    public DateTime VisitDate { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string ServiceRequired { get; set; } = string.Empty;
    public string? Referral { get; set; }
}
