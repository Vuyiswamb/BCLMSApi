namespace BCLMSApi.Models;

public class SystemUser
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public int PasswordIterations { get; set; }

    public bool IsActive { get; set; }

    public List<string> Groups { get; set; } = [];

    public bool HasAllRegions { get; set; } = true;

    public List<string> Regions { get; set; } = [];
}
