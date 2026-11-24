using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static PostAgent.Tests.TestData;
using PostAgent.Alerts;
using PostAgent.Posts;
using PostAgent.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Alerts;

public class AlertFunctionTests
{
    private readonly RecordingNotifier _notifier = new();

    private AlertFunction CreateFunction(InMemoryPostRepository repo, Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions { AlertSources = "chat", AlertTimeZone = "UTC" };
        configure?.Invoke(options);
        return new AlertFunction(repo, _notifier, Microsoft.Extensions.Options.Options.Create(options), new FakeTimeProvider(Now), NullLogger<AlertFunction>.Instance);
    }

    private static PostEntity HotChatPost(string rowKey = "chat_1") =>
        Post(rowKey, score: 9, hoursAgo: 1, type: SourceType.Group, source: "chat");

    [Fact]
    public async Task Sends_an_alert_and_marks_the_posts_as_sent_with_the_alert_time()
    {
        var repo = new InMemoryPostRepository(HotChatPost());

        await CreateFunction(repo).Run(new TimerInfo(), CancellationToken.None);

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

        await CreateFunction(repo).Run(new TimerInfo(), CancellationToken.None);

        Assert.Empty(_notifier.Sent);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task Stays_silent_during_quiet_hours_and_leaves_the_posts_for_the_digest()
    {
        var repo = new InMemoryPostRepository(HotChatPost());

        await CreateFunction(repo, o => o.AlertQuietHours = "06:00-10:00").Run(new TimerInfo(), CancellationToken.None); // зараз 08:00 UTC

        Assert.Empty(_notifier.Sent);
        Assert.Empty(repo.SentMarks);
        Assert.Null(repo.QueriedSince);
    }

    [Fact]
    public async Task Ignores_channels()
    {
        var repo = new InMemoryPostRepository(Post("chat_1", score: 10, hoursAgo: 1, type: SourceType.Channel, source: "chat"));

        await CreateFunction(repo).Run(new TimerInfo(), CancellationToken.None);

        Assert.Empty(_notifier.Sent);
    }
}
