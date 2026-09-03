namespace BCLMSApi.Models;

public class LoginResponse
{
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> Groups { get; set; } = [];

    public bool IsSuperUser { get; set; }

    public bool HasAllRegions { get; set; }

    public List<string> Regions { get; set; } = [];

    public string Token { get; set; } = string.Empty;
}
