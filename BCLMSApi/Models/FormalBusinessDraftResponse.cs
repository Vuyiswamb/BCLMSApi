namespace BCLMSApi.Models;

public class FormalBusinessDraftResponse
{
    public string ApplicationNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; }
}
