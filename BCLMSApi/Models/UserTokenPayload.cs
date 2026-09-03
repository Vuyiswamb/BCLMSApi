namespace BCLMSApi.Models;

public class UserTokenPayload
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> Groups { get; set; } = [];

    public bool HasAllRegions { get; set; } = true;

    public List<string> Regions { get; set; } = [];

    public long ExpiresAtUtc { get; set; }

    public bool IsCustomer => Groups.Contains("Customer", StringComparer.OrdinalIgnoreCase);

    public bool IsSuperUser => Groups.Contains("Super User", StringComparer.OrdinalIgnoreCase);
}
