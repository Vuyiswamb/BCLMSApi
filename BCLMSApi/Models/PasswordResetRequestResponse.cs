namespace BCLMSApi.Models;

public class PasswordResetRequestResponse
{
    public string Message { get; set; } = string.Empty;

    public string? ResetLink { get; set; }
}
