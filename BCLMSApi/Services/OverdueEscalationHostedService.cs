using System.Net;
using BCLMSApi.Data;

namespace BCLMSApi.Services;

public sealed class OverdueEscalationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OverdueEscalationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            logger.LogInformation("Weekly overdue escalation email run scheduled in {Delay}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await SendOverdueEscalationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Weekly overdue escalation email run failed.");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }

    private async Task SendOverdueEscalationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var datalayer = scope.ServiceProvider.GetRequiredService<Datalayer>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var recipients = await GetRecipientsAsync(datalayer, cancellationToken);
        var sent = 0;

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await emailService.SendEmailOffice365Async(
                    recipient.EmailAddress,
                    $"BCLMS escalation: overdue application {recipient.TrackingNumber}",
                    BuildEmailBody(recipient));

                await RecordReminderAsync(datalayer, recipient, cancellationToken);
                sent++;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to send overdue escalation for {TrackingNumber} to {EmailAddress}.", recipient.TrackingNumber, recipient.EmailAddress);
            }
        }

        logger.LogInformation("Weekly overdue escalation email run completed. {Sent} email(s) sent from {RecipientCount} overdue recipient(s).", sent, recipients.Count);
    }

    private static async Task<List<OverdueRecipient>> GetRecipientsAsync(Datalayer datalayer, CancellationToken cancellationToken)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_GetOverdueApplicationReminderRecipients");
        await command.Connection!.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var recipients = new List<OverdueRecipient>();

        while (await reader.ReadAsync(cancellationToken))
        {
            recipients.Add(new OverdueRecipient(
                reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                reader.GetString(reader.GetOrdinal("TrackingNumber")),
                reader.GetString(reader.GetOrdinal("BusinessName")),
                reader.GetString(reader.GetOrdinal("CurrentStage")),
                reader.GetInt32(reader.GetOrdinal("UserId")),
                reader.GetString(reader.GetOrdinal("DisplayName")),
                reader.GetString(reader.GetOrdinal("EmailAddress"))));
        }

        return recipients;
    }

    private static async Task RecordReminderAsync(Datalayer datalayer, OverdueRecipient recipient, CancellationToken cancellationToken)
    {
        await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_RecordOverdueApplicationReminder");
        command.Parameters.AddWithValue("@ApplicationId", recipient.ApplicationId);
        command.Parameters.AddWithValue("@UserId", recipient.UserId);
        await command.Connection!.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var dayName = configuration["OverdueEscalation:DayOfWeek"] ?? "Monday";
        var timeText = configuration["OverdueEscalation:Time"] ?? "08:00";
        var day = Enum.TryParse<DayOfWeek>(dayName, true, out var configuredDay) ? configuredDay : DayOfWeek.Monday;
        var time = TimeSpan.TryParse(timeText, out var configuredTime) ? configuredTime : TimeSpan.FromHours(8);
        var now = DateTime.Now;
        var candidate = now.Date.AddDays((7 + (int)day - (int)now.DayOfWeek) % 7).Add(time);
        if (candidate <= now) candidate = candidate.AddDays(7);
        return candidate - now;
    }

    private static string BuildEmailBody(OverdueRecipient recipient)
    {
        var trackingNumber = WebUtility.HtmlEncode(recipient.TrackingNumber);
        var businessName = WebUtility.HtmlEncode(recipient.BusinessName);
        var stage = WebUtility.HtmlEncode(recipient.CurrentStage);
        var officialName = WebUtility.HtmlEncode(recipient.DisplayName);
        return $"""
            <p>Dear {officialName},</p>
            <p>This is a weekly escalation for an overdue BCLMS application assigned to your department.</p>
            <p><strong>Tracking number:</strong> {trackingNumber}<br />
            <strong>Business:</strong> {businessName}<br />
            <strong>Current stage:</strong> {stage}</p>
            <p>Please review and action this application as soon as possible.</p>
            <p>This is an automated notification from BCLMS.</p>
            """;
    }

    private sealed record OverdueRecipient(
        int ApplicationId,
        string TrackingNumber,
        string BusinessName,
        string CurrentStage,
        int UserId,
        string DisplayName,
        string EmailAddress);
}
