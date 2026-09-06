namespace BCLMSApi.Models;

public class EventPremiseResponse
{
    public int EventPremiseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StreetNameAndNumber { get; set; } = string.Empty;
    public string ContactDetails { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsActive { get; set; }
}

public class EventPremiseSaveRequest
{
    public string Name { get; set; } = string.Empty;
    public string StreetNameAndNumber { get; set; } = string.Empty;
    public string ContactDetails { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
