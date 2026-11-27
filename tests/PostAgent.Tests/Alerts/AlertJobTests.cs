using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Alerts;

public class AlertJobTests
{
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();

    private AlertJob CreateJob(InMemoryPostRepository repo, Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions { AlertSources = "chat", AlertTimeZone = "UTC" };
        configure?.Invoke(options);
        return new AlertJob(repo, _notifier, new FakeEffectiveOptions(options), _embeddings, new FakeTimeProvider(Now), NullLogger<AlertJob>.Instance);
    }

    private static PostEntity HotChatPost(string rowKey = "chat_1") =>
        Post(rowKey, score: 9, hoursAgo: 1, type: SourceType.Group, source: "chat");

    [Fact]
    public async Task Takes_one_snapshot_of_the_effective_options_and_uses_its_threshold()
    {
        var effective = new FakeEffectiveOptions(new AgentOptions { AlertSources = "chat", AlertTimeZone = "UTC", AlertThreshold = 10, BurstMinPosts = 1000 });
        var job = new AlertJob(new InMemoryPostRepository(HotChatPost()), _notifier, effective, _embeddings, new FakeTimeProvider(Now), NullLogger<AlertJob>.Instance);

        await job.RunAsync(CancellationToken.None);

        Assert.Empty(_notifier.Sent);
        Assert.Equal(1, effective.Reads);
    }

    [Fact]
    public async Task Sends_an_alert_and_marks_the_posts_as_sent_with_the_alert_time()
    {
        var repo = new InMemoryPostRepository(HotChatPost());

        await CreateJob(repo).RunAsync(CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Тема", message);
        var (marked, alertedAt) = Assert.Single(repo.SentMarks);
        Assert.Equal(["chat_1"], marked.Select(p => p.RowKey));
        Assert.Equal(Now, alertedAt);
    }

    [Fact]
    public async Task Does_nothing_when_no_post_deserves_an_alert()
    {
        var repo = new InMemoryPostRepository(Post("chat_1", score: 5, hoursAgo: 1, type: SourceType.Group, source: "chat"));

        await CreateJob(repo).RunAsync(CancellationToken.None);

        Assert.Empty(_notifier.Sent);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task Stays_silent_during_quiet_hours_and_leaves_the_posts_for_the_digest()
    {
        var repo = new InMemoryPostRepository(HotChatPost());

        await CreateJob(repo, o => o.AlertQuietHours = "06:00-10:00").RunAsync(CancellationToken.None); // зараз 08:00 UTC

        Assert.Empty(_notifier.Sent);
        Assert.Empty(repo.SentMarks);
        Assert.Null(repo.QueriedSince);
    }

    [Fact]
    public async Task Ignores_channels()
    {
        var repo = new InMemoryPostRepository(Post("chat_1", score: 10, hoursAgo: 1, type: SourceType.Channel, source: "chat"));

        await CreateJob(repo).RunAsync(CancellationToken.None);

        Assert.Empty(_notifier.Sent);
    }
}
