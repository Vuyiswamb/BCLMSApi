namespace BCLMSApi.Services;

public interface ISmsService
{
    Task SendSmsAsync(string mobileNumber, string message);
}
