namespace BCLMSApi.Models;

public class ResetUser
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? EmailAddress { get; set; }
}
