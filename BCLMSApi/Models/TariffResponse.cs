namespace BCLMSApi.Models;

public class TariffResponse
{
    public int TariffId { get; set; }

    public string LicenceType { get; set; } = string.Empty;

    public string ApplicationKind { get; set; } = string.Empty;

    public decimal FeeAmount { get; set; }

    public bool IsActive { get; set; }
}
