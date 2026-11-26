using Microsoft.Extensions.Options;
using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Cleanup;

// Видаляє з Posts дні старші за RetentionDays, але не пізніше за останній день, що має підсумок (SummarizedThrough).
// Posts розбита на партиції-дні, тож з RetentionDays = 2 лишаються позавчорашня, вчорашня й сьогоднішня партиції.
// ChannelState (курсори читання) не чіпаємо: без неї Collector почав би читати джерела з нуля.
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
