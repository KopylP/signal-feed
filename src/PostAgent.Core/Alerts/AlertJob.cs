using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Alerts;

public sealed class AlertJob(
    IPostRepository posts,
    ITelegramNotifier notifier,
    IEffectiveOptions options,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
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

        // The followed topics look further back than the groups' alert window; the regular selection filters by its own window anyway.
        var since = AlertSelector.LookbackStart(settings, now);
        if (settings.WatchList.Count > 0 && now - AlertSelector.WatchWindow < since)
        {
            since = now - AlertSelector.WatchWindow;
        }

        var recent = await posts.GetSinceAsync(since, now, ct);
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
        var vectors = await TryEmbedWatchesAsync(settings.WatchList, recent, ct);
        var watched = AlertSelector.SelectWatched(recent, settings, now, alerts.Select(a => a.RowKey).ToHashSet(), vectors);
        if (watched.Count == 0)
        {
            return;
        }

        await notifier.SendAsync(WatchAlertMessage.Format(watched), ct);
        await posts.MarkSentAsync(watched.Select(h => h.Post), alertedAt: null, ct);
        log.LogInformation("Watch alert sent: {Count} posts", watched.Count);
    }

    // The topics are embedded on every run (one small call), so a new topic also matches the posts of the last day at once.
    // Without vectors the word match still works.
    private async Task<IReadOnlyDictionary<string, float[]>> TryEmbedWatchesAsync(IReadOnlyList<string> watches, IReadOnlyList<PostEntity> recent, CancellationToken ct)
    {
        if (watches.Count == 0 || !recent.Any(p => p.Embedding is not null))
        {
            return new Dictionary<string, float[]>();
        }

        try
        {
            var vectors = await embeddings.EmbedAsync(watches, ct);
            return watches.Zip(vectors).ToDictionary(x => x.First, x => x.Second);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The followed topics could not be embedded, matching them by words only");
            return new Dictionary<string, float[]>();
        }
    }
}
