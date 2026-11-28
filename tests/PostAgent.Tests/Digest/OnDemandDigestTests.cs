using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Digest;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class OnDemandDigestTests
{
    private const long Asking = 42;

    private readonly RecordingNotifier _notifier = new();

    private static readonly DigestRequest Day = new(Asking, Now.AddHours(-24), Now);

    private OnDemandDigest Create(IPostRepository repo, IDigestSummarizer summarizer) =>
        new(repo, summarizer, _notifier, new FakeEffectiveOptions(new AgentOptions { TimeZone = "UTC" }), NullLogger<OnDemandDigest>.Instance);

    private static IDigestSummarizer Overview(string overview) =>
        new FakeDigestSummarizer(items => new DigestReport(
            overview, items.Select(i => new DigestEntry(i.Post, "Заголовок", "Пояснення", "Чому")).ToList(), [], [], [], [], DigestSkipped.None));

    [Fact]
    public async Task Sends_the_digest_of_the_period_only_to_the_chat_that_asked_and_marks_nothing()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await Create(repo, Overview("Огляд")).RunAsync(Day, CancellationToken.None);

        Assert.Empty(_notifier.Sent);
        var (chat, html) = Assert.Single(_notifier.SentTo);
        Assert.Equal(Asking, chat);
        Assert.Contains("Огляд", html);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task Includes_posts_a_daily_digest_already_sent_but_not_duplicates_or_unscored_ones()
    {
        var sent = Post("sent", score: 9, hoursAgo: 2, status: PostStatus.Sent);
        var repo = new InMemoryPostRepository(sent, Post("dup", score: 9, hoursAgo: 2, duplicateOf: "sent"), Unscored("new"));
        IReadOnlyList<DigestItem>? seen = null;
        var summarizer = new FakeDigestSummarizer(items =>
        {
            seen = items;
            return DigestReport.Plain(items);
        });

        await Create(repo, summarizer).RunAsync(Day, CancellationToken.None);

        Assert.Equal(["sent"], seen!.Select(i => i.Post.RowKey));
    }

    [Fact]
    public async Task Only_posts_published_inside_the_period_are_used()
    {
        var repo = new InMemoryPostRepository(Post("in", score: 7, hoursAgo: 2), Post("out", score: 9, hoursAgo: 30));
        IReadOnlyList<DigestItem>? seen = null;
        var summarizer = new FakeDigestSummarizer(items =>
        {
            seen = items;
            return DigestReport.Plain(items);
        });

        await Create(repo, summarizer).RunAsync(Day, CancellationToken.None);

        Assert.Equal(["in"], seen!.Select(i => i.Post.RowKey));
    }

    [Fact]
    public async Task Says_so_when_the_period_has_no_scored_posts()
    {
        await Create(new InMemoryPostRepository(), Overview("О")).RunAsync(Day, CancellationToken.None);

        Assert.Contains("оцінених постів не знайшов", Assert.Single(_notifier.Replies));
    }

    [Fact]
    public async Task Still_sends_the_posts_when_the_model_fails()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await Create(repo, new FakeDigestSummarizer(_ => throw new InvalidOperationException("model is down"))).RunAsync(Day, CancellationToken.None);

        Assert.Contains("Причина", Assert.Single(_notifier.Replies));
    }

    [Fact]
    public async Task A_failure_is_told_to_the_reader_and_not_thrown()
    {
        var repo = new FailingRepository();

        await Create(repo, Overview("О")).RunAsync(Day, CancellationToken.None);

        Assert.Equal(DigestTexts.Failed, Assert.Single(_notifier.Replies));
    }

    private sealed class FailingRepository : IPostRepository
    {
        public Task AddIfMissingAsync(PostEntity post, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task SaveEmbeddingsAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task MarkAlertDecidedAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct) => throw new InvalidOperationException("storage is down");
    }
}
