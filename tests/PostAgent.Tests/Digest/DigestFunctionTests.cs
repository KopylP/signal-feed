using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Digest;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestFunctionTests
{
    private readonly RecordingNotifier _notifier = new();

    private DigestFunction CreateFunction(InMemoryPostRepository repo, IDigestSummarizer summarizer) =>
        new(repo, summarizer, _notifier, Microsoft.Extensions.Options.Options.Create(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DigestFunction>.Instance);

    private static IDigestSummarizer WithOverview(string overview) =>
        new FakeDigestSummarizer(items => new DigestReport(overview, items.Select(i => i with { Insight = "Пояснення" }).ToList()));

    private static IDigestSummarizer Failing() =>
        new FakeDigestSummarizer(_ => throw new InvalidOperationException("model is down"));

    [Fact]
    public async Task Sends_the_no_posts_message_when_there_is_nothing_to_send()
    {
        var repo = new InMemoryPostRepository();

        await CreateFunction(repo, WithOverview("О")).Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal([DigestMessage.NoPosts], _notifier.Sent);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task Sends_the_overview_with_insights_and_marks_the_posts_as_sent()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2), Post("b", score: 4, hoursAgo: 3));

        await CreateFunction(repo, WithOverview("Огляд дня")).Run(new TimerInfo(), CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Огляд дня", message);
        Assert.Contains("Пояснення", message);
        var (marked, alertedAt) = Assert.Single(repo.SentMarks);
        Assert.Equal(["a", "b"], marked.Select(p => p.RowKey));
        Assert.Null(alertedAt);
    }

    [Fact]
    public async Task Still_sends_the_digest_without_the_overview_when_the_model_fails()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await CreateFunction(repo, Failing()).Run(new TimerInfo(), CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Причина", message); // коротка причина оцінки замість пояснення
        Assert.Single(repo.SentMarks);
    }

    [Fact]
    public async Task Reads_posts_from_the_start_of_the_digest_lookback()
    {
        var repo = new InMemoryPostRepository();

        await CreateFunction(repo, WithOverview("О")).Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal(DigestSelector.QueryStart(Now), repo.QueriedSince);
    }
}
