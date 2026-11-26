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
        if (alerts.Count > 0)
        {
            await notifier.SendAsync(AlertMessage.Format(alerts), ct);

            // Sent keeps the digest from repeating these posts; AlertedAt is the pause between alerts for a group.
            await posts.MarkSentAsync(alerts, alertedAt: now, ct);
            log.LogInformation("Alert sent: {Count} posts", alerts.Count);
        }

        await SendWatchedAsync(recent, alerts, settings, now, ct);
    }

    // Followed topics: their own message, no cooldown for the source (AlertedAt stays empty).
    private async Task SendWatchedAsync(IReadOnlyList<PostEntity> recent, IReadOnlyList<PostEntity> alerts, AgentOptions settings, DateTimeOffset now, CancellationToken ct)
    {
        var watched = AlertSelector.SelectWatched(recent, settings, now, alerts.Select(a => a.RowKey).ToHashSet());
        if (watched.Count == 0)
        {
            return;
        }

        await notifier.SendAsync(WatchAlertMessage.Format(watched), ct);
        await posts.MarkSentAsync(watched.Select(h => h.Post), alertedAt: null, ct);
        log.LogInformation("Watch alert sent: {Count} posts", watched.Count);
    }
}
