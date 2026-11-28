using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Collecting;

// Collects one source: reads new messages, stores the posts, queues them for the embedding stage, moves the cursor.
public sealed class SourceCollector(
    ISourceReader reader,
    IPostRepository posts,
    ISourceCursors cursors,
    [FromKeyedServices(StorageNames.DeduplicationQueue)] IPostQueue queue,
    SettingsEditor editor,
    ILogger<SourceCollector> log)
{
    public async Task CollectAsync(string username, AgentOptions settings, CancellationToken ct)
    {
        var batch = await reader.ReadAsync(username, await cursors.GetLastPostIdAsync(username, ct), ct);
        var newPosts = PostBuilder.Build(batch.Source, batch.Messages, settings);

        foreach (var post in newPosts)
        {
            await posts.AddIfMissingAsync(post, ct);
        }

        // The order matters: posts and the queue first, the cursor last. If a run is cut off,
        // the next one reads the same messages, and the embedding stage and the scorer skip the posts that are already done.
        await queue.EnqueueAsync(newPosts.Select(p => p.ToRef()).ToList(), settings.ScoringBatchSize, ct);
        await cursors.SetLastPostIdAsync(username, batch.LastPostId, ct);
        await TryMarkVerifiedAsync(username, ct);

        log.LogInformation("@{Source} ({Type}): {Messages} messages, {Posts} posts",
            username, batch.Source.Type, batch.Messages.Count, newPosts.Count);
    }

    // A read that ended without an error confirms a source added from the bot. Never worth failing the collection for.
    private async Task TryMarkVerifiedAsync(string username, CancellationToken ct)
    {
        try
        {
            await editor.MarkSourceVerifiedAsync(username, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "@{Source} was read, but it could not be marked as verified", username);
        }
    }
}
