using Microsoft.Extensions.Options;
using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Cleanup;

// Deletes the days of Posts older than RetentionDays, but never past the last day that has a summary (SummarizedThrough).
// The retention is always at most yesterday, so a request that names a later day cannot make it delete more.
// Posts is partitioned by day, so with RetentionDays = 2 the day before yesterday, yesterday and today stay.
// ChannelState (the read cursors) is not touched: without it the Collector would read the sources from the start.
public sealed class PostsCleanup(IPostRepository posts, IOptions<AgentOptions> options) : ICleanupTask
{
    public string Name => "Posts";

    public Task<int> RunAsync(CleanupRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var retention = now.AddDays(-options.Value.RetentionDays);
        var firstUnsummarized = new DateTimeOffset(request.Day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return posts.DeleteOlderThanAsync(retention < firstUnsummarized ? retention : firstUnsummarized, ct);
    }
}
