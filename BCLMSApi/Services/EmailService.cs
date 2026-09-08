namespace BCLMSApi.Services;

public class EmailService : IEmailService
{
    public Task SendEmailOffice365Async(string to, string subject, string body)
    {
        // Use the existing, verified Office 365 sender while the SMTP password settings are corrected.
        return GenericMethods.SendEmail_Office365Async(to, subject, body);
    }
}