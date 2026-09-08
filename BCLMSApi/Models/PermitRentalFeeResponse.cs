namespace BCLMSApi.Models;

public class PermitRentalFeeResponse
{
    public int PermitRentalFeeId { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public string? TradingLocation { get; set; }
    public decimal MonthlyFee { get; set; }

    public bool IsActive { get; set; }
}
