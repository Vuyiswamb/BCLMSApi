namespace BCLMSApi.Models;

public class FormalBusinessDraftRequest
{
    public string? TradeName { get; set; }

    public DateTime? ApplicationDate { get; set; }

    public string? ApplicationNumber { get; set; }

    public string? LicenceType { get; set; }

    public string? WardNumber { get; set; }

    public string? Area { get; set; }

    public string? PreparedBy { get; set; }

    public string? Representative { get; set; }

    public string? Comments { get; set; }

    public List<FormalBusinessRequirementItem> Requirements { get; set; } = [];

    public List<FormalBusinessDiaryItem> Diaries { get; set; } = [];
}
