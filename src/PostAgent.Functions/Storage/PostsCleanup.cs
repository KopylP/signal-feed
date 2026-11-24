using Microsoft.Extensions.Options;

namespace PostAgent.Storage;

/// <summary>
/// Видаляє з Posts дні старші за RetentionDays. Posts розбита на партиції-дні, тож з RetentionDays = 1
/// лишаються вчорашня й сьогоднішня партиції. ChannelState (курсори читання) не чіпаємо: без неї
/// Collector почав би читати джерела з нуля.
/// </summary>
public sealed class PostsCleanup(PostRepository posts, IOptions<AgentOptions> options) : ICleanupTask
{
    public string Name => "Posts";

    public Task<int> RunAsync(DateTimeOffset now, CancellationToken ct) =>
        posts.DeleteOlderThanAsync(now.AddDays(-options.Value.RetentionDays), ct);
}
