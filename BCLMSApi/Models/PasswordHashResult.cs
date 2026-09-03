namespace BCLMSApi.Models;

public class PasswordHashResult
{
    public string Hash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public int Iterations { get; set; }
}
