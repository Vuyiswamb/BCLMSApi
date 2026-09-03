namespace BCLMSApi.Models;

public class TownshipResponse
{
    public int TownshipId { get; set; }

    public string TownshipName { get; set; } = string.Empty;

    public string WardNumber { get; set; } = string.Empty;

    public string? RegionName { get; set; }

    public string? AreaCategory { get; set; }
}
