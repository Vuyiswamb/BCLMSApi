namespace BCLMSApi.Models;

public class TariffSaveRequest
{
    public string LicenceType { get; set; } = string.Empty;

    public string ApplicationKind { get; set; } = "New";

    public decimal FeeAmount { get; set; }

    public bool IsActive { get; set; } = true;
}
