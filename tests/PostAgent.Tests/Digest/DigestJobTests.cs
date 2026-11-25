using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Digest;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestJobTests
{
    private RecordingNotifier _notifier = new();

    private DigestJob CreateJob(InMemoryPostRepository repo, IDigestSummarizer summarizer) =>
        new(repo, summarizer, _notifier, Microsoft.Extensions.Options.Options.Create(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DigestJob>.Instance);

    private static IDigestSummarizer WithOverview(string overview, string text = "Пояснення") =>
        new FakeDigestSummarizer(items => new DigestReport(
            overview,
            items.Select(i => new DigestEntry(i.Post, "Заголовок", text, "Чому важливо")).ToList(),
            [], [], [], [], DigestSkipped.None));

    private static IDigestSummarizer Failing() =>
        new FakeDigestSummarizer(_ => throw new InvalidOperationException("model is down"));

    [Fact]
    public async Task Sends_the_no_posts_message_when_there_is_nothing_to_send()
    {
        var repo = new InMemoryPostRepository();

        await CreateJob(repo, WithOverview("О")).RunAsync(CancellationToken.None);

        Assert.Equal([DigestMessage.NoPosts], _notifier.Sent);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task Sends_the_overview_with_the_entries_and_marks_the_posts_as_sent()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2), Post("b", score: 4, hoursAgo: 3));

        await CreateJob(repo, WithOverview("Огляд дня")).RunAsync(CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Огляд дня", message);
        Assert.Contains("Пояснення", message);
        var (marked, alertedAt) = Assert.Single(repo.SentMarks);
        Assert.Equal(["a", "b"], marked.Select(p => p.RowKey));
        Assert.Null(alertedAt);
    }

    [Fact]
    public async Task Tells_how_many_posts_were_left_out_of_the_digest()
    {
        var repo = new InMemoryPostRepository(
            Post("a", score: 9, hoursAgo: 2),
            Post("copy", score: 4, hoursAgo: 3, duplicateOf: "a"));

        await CreateJob(repo, WithOverview("Огляд")).RunAsync(CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Можна пропустити", message);
        Assert.Contains("повторів — 1", message);
    }

    [Fact]
    public async Task Still_sends_the_digest_without_the_overview_when_the_model_fails()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await CreateJob(repo, Failing()).RunAsync(CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Причина", message); // the short scoring reason instead of the model's text
        Assert.Single(repo.SentMarks);
    }

    [Fact]
    public async Task Sends_a_long_digest_as_several_messages_and_marks_the_posts_once_at_the_end()
    {
        var posts = Enumerable.Range(0, 7).Select(i => Post($"p{i}", score: 9, hoursAgo: 2 + i)).ToArray();
        var repo = new InMemoryPostRepository(posts);

        await CreateJob(repo, WithOverview("Огляд", text: new string('с', 700))).RunAsync(CancellationToken.None);

        Assert.True(_notifier.Sent.Count > 1);
        Assert.Single(repo.SentMarks);
    }

    [Fact]
    public async Task Does_not_mark_the_posts_as_sent_when_a_later_message_fails()
    {
        _notifier = new RecordingNotifier { FailOnCall = 2 };
        var posts = Enumerable.Range(0, 7).Select(i => Post($"p{i}", score: 9, hoursAgo: 2 + i)).ToArray();
        var repo = new InMemoryPostRepository(posts);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob(repo, WithOverview("Огляд", text: new string('с', 700))).RunAsync(CancellationToken.None));

        Assert.Empty(repo.SentMarks); // the next run repeats the digest: a duplicate is better than a lost one
    }

    [Fact]
    public async Task Warns_about_unscored_posts_instead_of_saying_the_day_was_quiet()
    {
        var repo = new InMemoryPostRepository(Post("stuck", score: null, hoursAgo: 3, status: PostStatus.New));

        await CreateJob(repo, WithOverview("О")).RunAsync(CancellationToken.None);

        Assert.Equal([DigestMessage.Unscored(1)], _notifier.Sent);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task Warns_about_unscored_posts_after_the_digest()
    {
        var repo = new InMemoryPostRepository(
            Post("a", score: 9, hoursAgo: 2),
            Post("stuck", score: null, hoursAgo: 3, status: PostStatus.New));

        await CreateJob(repo, WithOverview("Огляд")).RunAsync(CancellationToken.None);

        Assert.Equal(2, _notifier.Sent.Count);
        Assert.Contains("Огляд", _notifier.Sent[0]);
        Assert.Equal(DigestMessage.Unscored(1), _notifier.Sent[1]);
        Assert.Single(repo.SentMarks);
    }

    [Fact]
    public async Task Reads_posts_from_the_start_of_the_digest_lookback()
    {
        var repo = new InMemoryPostRepository();

        await CreateJob(repo, WithOverview("О")).RunAsync(CancellationToken.None);

        Assert.Equal(DigestSelector.QueryStart(Now), repo.QueriedSince);
    }
}
