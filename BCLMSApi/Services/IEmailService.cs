namespace BCLMSApi.Services;

public interface IEmailService
{
    Task SendEmailOffice365Async(string to, string subject, string body);
}
