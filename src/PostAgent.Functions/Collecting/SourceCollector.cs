using Microsoft.Extensions.Options;
using PostAgent.Scoring;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Collecting;

// Collects one source: reads new messages, stores the posts, queues them for scoring, moves the cursor.
public sealed class SourceCollector(
    ISourceReader reader,
    IPostRepository posts,
    ISourceCursors cursors,
    IScoringQueue queue,
    IOptions<AgentOptions> options,
    ILogger<SourceCollector> log)
{
    public async Task CollectAsync(string username, CancellationToken ct)
    {
        var batch = await reader.ReadAsync(username, await cursors.GetLastPostIdAsync(username, ct), ct);
        var newPosts = PostBuilder.Build(batch.Source, batch.Messages, options.Value);

        foreach (var post in newPosts)
        {
            await posts.AddIfMissingAsync(post, ct);
        }

        // The order matters: posts and the queue first, the cursor last. If a run is cut off,
        // the next one reads the same messages and the scorer skips the posts that are already scored.
        await queue.EnqueueAsync(newPosts.Select(p => p.ToRef()).ToList(), options.Value.ScoringBatchSize, ct);
        await cursors.SetLastPostIdAsync(username, batch.LastPostId, ct);

        log.LogInformation("@{Source} ({Type}): {Messages} messages, {Posts} posts",
            username, batch.Source.Type, batch.Messages.Count, newPosts.Count);
    }
}
