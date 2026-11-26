using Microsoft.Extensions.Options;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Cleanup;

// Видаляє з Posts дні старші за RetentionDays. Posts розбита на партиції-дні, тож з RetentionDays = 2
// лишаються позавчорашня, вчорашня й сьогоднішня партиції. ChannelState (курсори читання) не чіпаємо: без неї
// Collector почав би читати джерела з нуля.
public sealed class PostsCleanup(IPostRepository posts, IOptions<AgentOptions> options) : ICleanupTask
{
    public string Name => "Posts";

    public Task<int> RunAsync(DateTimeOffset now, CancellationToken ct) =>
        posts.DeleteOlderThanAsync(now.AddDays(-options.Value.RetentionDays), ct);
}
