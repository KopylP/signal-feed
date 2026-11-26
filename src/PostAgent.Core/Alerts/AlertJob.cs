using PostAgent.Shared.Jobs;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Alerts;

public sealed class AlertJob(
    IPostRepository posts,
    ITelegramNotifier notifier,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<AlertJob> log) : IJob
{
    public string Name => "alert";

    public async Task RunAsync(CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
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

        await notifier.SendAsync(AlertMessage.Format(alerts), ct);

        // Sent keeps the digest from repeating these posts; AlertedAt is the pause between alerts for a group.
        await posts.MarkSentAsync(alerts, alertedAt: now, ct);
        log.LogInformation("Alert sent: {Count} posts", alerts.Count);
    }
}
