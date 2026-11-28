using PostAgent.Shared.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Topics;

namespace PostAgent.Alerts;

// Chooses what to alert about and publishes only that (ADR-0024). One decision at a time: the posts are read again under the lock,
// so a mark another decision set is seen, and the marks are saved before the queue, so a retry never decides the same post twice.
public sealed class AlertDecider(
    IPostRepository posts,
    IFollowedTopicStore topics,
    IAlertDecisionLock decisionLock,
    IAlertQueue queue,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<AlertDecider> log) : IAlertDecider
{
    public async Task DecideAsync(IReadOnlyList<PostEntity> scored, CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var now = time.GetUtcNow();
        if (scored.Count == 0 || IsQuiet(settings, now))
        {
            return;
        }

        var vectors = settings.WatchList.Count > 0 ? await TryLoadVectorsAsync(ct) : new Dictionary<string, float[]>();
        await using (await decisionLock.AcquireAsync(ct))
        {
            var current = await posts.GetAsync(scored.Select(p => p.ToRef()), ct);
            // A group post is alerted only inside the alert window, so a post alerted within the cooldown was published within both.
            var recent = await posts.GetSinceAsync(now.AddHours(-(settings.AlertWindowHours + settings.AlertCooldownHours)), now, ct);
            var group = AlertSelector.SelectGroup(current, recent, settings, now);
            var watched = AlertSelector.SelectWatched(current.Except(group), settings.WatchList, vectors, settings, now);
            await PublishAsync(group, watched, now, ct);
        }
    }

    public async Task DecideTopicAsync(string phrase, float[] vector, CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var now = time.GetUtcNow();
        if (IsQuiet(settings, now))
        {
            return;
        }

        await using (await decisionLock.AcquireAsync(ct))
        {
            var recent = await posts.GetSinceAsync(now - AlertSelector.WatchWindow, now, ct);
            var watched = AlertSelector.SelectWatched(recent, [phrase], new Dictionary<string, float[]> { [phrase] = vector }, settings, now);
            await PublishAsync([], watched, now, ct);
        }
    }

    private bool IsQuiet(AgentOptions settings, DateTimeOffset now)
    {
        if (!QuietHours.IsQuiet(settings.AlertQuietHours, settings.AlertTimeZone, now))
        {
            return false;
        }

        log.LogInformation("Quiet hours, no alert is published (the posts stay for the digest)");
        return true;
    }

    private async Task PublishAsync(IReadOnlyList<PostEntity> group, IReadOnlyList<WatchHit> watched, DateTimeOffset now, CancellationToken ct)
    {
        if (group.Count == 0 && watched.Count == 0)
        {
            return;
        }

        foreach (var post in group)
        {
            post.AlertedAt = now; // also the start of the group's cooldown
        }

        foreach (var hit in watched)
        {
            hit.Post.WatchAlertedAt = now;
        }

        await posts.MarkAlertDecidedAsync(group.Concat(watched.Select(h => h.Post)), ct);
        if (group.Count > 0)
        {
            await queue.EnqueueAsync(new AlertRequest(AlertKind.Group, group.Select(p => new AlertedPost(p.PartitionKey, p.RowKey)).ToList()), ct);
        }

        if (watched.Count > 0)
        {
            await queue.EnqueueAsync(new AlertRequest(AlertKind.Topic, watched.Select(h => new AlertedPost(h.Post.PartitionKey, h.Post.RowKey, h.Phrase)).ToList()), ct);
        }

        log.LogInformation("Alerts decided: {Group} group posts, {Topic} followed-topic posts", group.Count, watched.Count);
    }

    // A topic without a vector still matches by words; a read failure must not cost the group alerts.
    private async Task<IReadOnlyDictionary<string, float[]>> TryLoadVectorsAsync(CancellationToken ct)
    {
        try
        {
            return await topics.LoadAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The vectors of the followed topics could not be read, matching them by words only");
            return new Dictionary<string, float[]>();
        }
    }
}
