namespace BCLMSApi.Models;

public class EmailSettingsResponse
{
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public string PasswordMask { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class EmailSettingsUpdateRequest
{
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
}
