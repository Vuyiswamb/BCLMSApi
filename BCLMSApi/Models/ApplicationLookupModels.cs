namespace BCLMSApi.Models;

public sealed class ApplicationLookupResponse
{
    public int LookupId { get; set; }
    public string LookupType { get; set; } = string.Empty;
    public string LookupValue { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ApplicationLookupSaveRequest
{
    public string LookupType { get; set; } = string.Empty;
    public string LookupValue { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
