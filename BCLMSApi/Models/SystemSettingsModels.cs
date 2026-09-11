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

public class HanisSettingsResponse
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool AllowInsecureQa { get; set; }
    public bool HasApiKey { get; set; }
    public string ApiKeyMask { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class HanisSettingsUpdateRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool AllowInsecureQa { get; set; }
    public string? ApiKey { get; set; }
}
