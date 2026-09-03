using System.Net;
using System.Net.Mail;

namespace BCLMSApi.Services;

public class EmailService(IConfiguration configuration, ISystemSettingsService settingsService) : IEmailService
{
    public async Task SendEmailOffice365Async(string to, string subject, string body)
    {
        var smtpHost = configuration["Email:Office365:Host"] ?? "smtp.office365.com";
        var smtpPort = int.TryParse(configuration["Email:Office365:Port"], out var configuredPort)
            ? configuredPort
            : 587;
        var fromAddress = configuration["Email:Office365:FromAddress"] ?? "Ithuba@TSHWANE.GOV.ZA";
        var username = configuration["Email:Office365:Username"] ?? fromAddress;
        var password = configuration["Email:Office365:Password"]
            ?? Environment.GetEnvironmentVariable("BCLMS_OFFICE365_PASSWORD")
            ?? await settingsService.GetEmailPasswordAsync();

        try
        {
            using var smtpClient = new SmtpClient(smtpHost, smtpPort);
            using var mailMessage = new MailMessage();

            mailMessage.From = new MailAddress(fromAddress);
            mailMessage.To.Add(new MailAddress(to));
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = true;

            smtpClient.EnableSsl = bool.TryParse(configuration["Email:Office365:EnableSsl"], out var enableSsl)
                ? enableSsl
                : true;
            smtpClient.UseDefaultCredentials = string.IsNullOrWhiteSpace(password);
            if (!string.IsNullOrWhiteSpace(password))
            {
                smtpClient.Credentials = new NetworkCredential(username, password);
            }
            smtpClient.Timeout = 60000;

            await smtpClient.SendMailAsync(mailMessage);
            Console.WriteLine($"Email sent successfully to: {to}");
        }
        catch (SmtpException error)
        {
            throw new InvalidOperationException($"SMTP error sending email to {to}: {error.Message}", error);
        }
    }
}
