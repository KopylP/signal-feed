using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace PostAgent.Functions;

/// <summary>Щогодини шукає в групах зі списку AlertSources сплески й дуже важливі повідомлення.</summary>
public sealed class AlertFunction(
    PostRepository posts,
    TelegramNotifier notifier,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<AlertFunction> log)
{
    [Function(nameof(AlertFunction))]
    public async Task Run([TimerTrigger("%AlertSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();

        if (QuietHours.IsQuiet(settings.AlertQuietHours, settings.AlertTimeZone, now))
        {
            log.LogInformation("Quiet hours, alerts skipped (posts stay for the digest)");
            return;
        }

        var recent = await posts.GetSinceAsync(AlertSelector.LookbackStart(settings, now), now, ct);
        var alerts = AlertSelector.Select(recent, settings, now);
        if (alerts.Count == 0)
        {
            return;
        }

        await notifier.SendAlertAsync(alerts, ct);

        // Sent, щоб дайджест не повторював ці пости; AlertedAt для паузи між алертами групи.
        await posts.MarkSentAsync(alerts, alertedAt: now, ct);
        log.LogInformation("Alert sent: {Count} posts", alerts.Count);
    }
}
