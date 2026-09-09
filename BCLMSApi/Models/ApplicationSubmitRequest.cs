namespace BCLMSApi.Models;

public class ApplicationSubmitRequest
{
    public int? BusinessId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string IdNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string? RegistrationNumber { get; set; }

    public string LicenceType { get; set; } = string.Empty;

    public bool? IsSpecialEvent { get; set; }

    public DateTime? EventStartDate { get; set; }

    public DateTime? EventEndDate { get; set; }

    public FoodVendingDetails? FoodVendingDetails { get; set; }

    public string? TradeStandBusinessType { get; set; }

    public string? PrePackedPerishableGoods { get; set; }

    public decimal? ApplicationFee { get; set; }

    public string? WardNumber { get; set; }

    public string? RegionName { get; set; }

    public string? AreaCategory { get; set; }

    public int? TownshipId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? PostalAddress { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Notes { get; set; }
}
