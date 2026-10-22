using System.Net;
using System.Net.Mail;

namespace BCLMSApi.Services;

public class EmailService(ISystemSettingsService systemSettings) : IEmailService
{
    public async Task SendEmailOffice365Async(string to, string subject, string body)
    {
        var settings = await systemSettings.GetEmailSettingsAsync();
        if (!int.TryParse(settings.Port, out var port) || string.IsNullOrWhiteSpace(settings.Host)
            || string.IsNullOrWhiteSpace(settings.FromAddress)
            || string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("Office 365 email settings are incomplete. Configure the SMTP host, port, sender, username, and password.");
        }

        using var smtpClient = new SmtpClient(settings.Host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            Timeout = 60000,
        };
        using var mailMessage = new MailMessage
        {
            From = new MailAddress(settings.FromAddress),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };
        mailMessage.To.Add(new MailAddress(to));

        try
        {
            await smtpClient.SendMailAsync(mailMessage);
        }
        catch (SmtpException)
        {
            throw;
        }
    }
}
