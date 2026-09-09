using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Net.Mail;

namespace BclmsOverdueReminderService;

public sealed class OverdueReminderWorker(
    IConfiguration configuration,
    EmailTemplateRenderer templates,
    ILogger<OverdueReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await SendAsync(token); }
            catch (Exception error) { logger.LogError(error, "Overdue reminder job failed."); }
            await Task.Delay(TimeSpan.FromHours(1), token);
        }
    }

    private async Task SendAsync(CancellationToken token)
    {
        var connectionString = configuration.GetConnectionString("BclmsConnection") ?? throw new InvalidOperationException("BclmsConnection is required.");
        await using var database = new SqlConnection(connectionString);
        await database.OpenAsync(token);

        foreach (var recipient in await GetRecipientsAsync(database, token))
        {
            var email = templates.Render("OverdueApplicationReminder.xml", new Dictionary<string, string?>
            {
                ["RecipientName"] = recipient.RecipientName,
                ["TrackingNumber"] = recipient.TrackingNumber,
                ["BusinessName"] = recipient.BusinessName,
                ["CurrentStage"] = recipient.CurrentStage
            });

            await SendEmailAsync(recipient.EmailAddress, email, token);
            await RecordSentReminderAsync(database, recipient, token);
        }
    }

    private static async Task<List<ReminderRecipient>> GetRecipientsAsync(SqlConnection database, CancellationToken token)
    {
        await using var command = new SqlCommand("dbo.usp_GetOverdueApplicationReminderRecipients", database) { CommandType = CommandType.StoredProcedure };
        await using var reader = await command.ExecuteReaderAsync(token);
        var recipients = new List<ReminderRecipient>();

        while (await reader.ReadAsync(token))
        {
            recipients.Add(new ReminderRecipient(
                reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                reader.GetInt32(reader.GetOrdinal("UserId")),
                reader["TrackingNumber"] as string ?? "Application",
                reader["BusinessName"] as string ?? "the business",
                reader["CurrentStage"] as string ?? "the current stage",
                reader["DisplayName"] as string ?? "Official",
                reader.GetString(reader.GetOrdinal("EmailAddress"))));
        }

        return recipients;
    }

    private async Task SendEmailAsync(string recipient, RenderedEmail email, CancellationToken token)
    {
        var settings = configuration.GetSection("Email:Office365");
        using var message = new MailMessage(settings["FromAddress"] ?? "BCR@TSHWANE.GOV.ZA", recipient) { Subject = email.Subject, IsBodyHtml = true, Body = email.HtmlBody };
        using var smtp = new SmtpClient(settings["Host"] ?? "smtp.office365.com", int.TryParse(settings["Port"], out var port) ? port : 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings["Username"], settings["Password"])
        };
        await smtp.SendMailAsync(message, token);
    }

    private static async Task RecordSentReminderAsync(SqlConnection database, ReminderRecipient recipient, CancellationToken token)
    {
        await using var command = new SqlCommand("dbo.usp_RecordOverdueApplicationReminder", database) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@ApplicationId", SqlDbType.Int).Value = recipient.ApplicationId;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = recipient.UserId;
        await command.ExecuteNonQueryAsync(token);
    }

    private sealed record ReminderRecipient(int ApplicationId, int UserId, string TrackingNumber, string BusinessName, string CurrentStage, string RecipientName, string EmailAddress);
}
