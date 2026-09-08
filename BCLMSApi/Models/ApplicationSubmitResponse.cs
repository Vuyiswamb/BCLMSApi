namespace BCLMSApi.Models;

public class ApplicationSubmitResponse
{
    public int ApplicationId { get; set; }

    public string TrackingNumber { get; set; } = string.Empty;

    public string ApplicantName { get; set; } = string.Empty;

    public string IdNumber { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string LicenceType { get; set; } = string.Empty;

    public DateTime? EventStartDate { get; set; }

    public DateTime? EventEndDate { get; set; }

    public FoodVendingDetails? FoodVendingDetails { get; set; }

    public string? TradeStandBusinessType { get; set; }

    public string CurrentStage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime SubmittedDate { get; set; }

    public string? RegionName { get; set; }

    public string? AreaOrSuburb { get; set; }

    public string? AreaCategory { get; set; }
}
