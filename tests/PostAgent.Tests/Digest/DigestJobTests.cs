using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Digest;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestJobTests
{
    private RecordingNotifier _notifier = new();
    private InMemoryChatStateStore _chatState = new();

    private static readonly PostAgent.Shared.Notifications.ChatResolver Chats = new(Microsoft.Extensions.Options.Options.Create(new AgentOptions()), new InMemoryChatBindingStore(TestData.ChatId));

    private DigestJob CreateJob(InMemoryPostRepository repo, IDigestSummarizer summarizer) =>
        new(repo, summarizer, _notifier, _chatState, Chats, new FakeEffectiveOptions(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DigestJob>.Instance);

    private static IDigestSummarizer WithOverview(string overview, string text = "Пояснення") =>
        new FakeDigestSummarizer(items => new DigestReport(
            overview,
            items.Select(i => new DigestEntry(i.Post, "Заголовок", text, "Чому важливо")).ToList(),
            [], [], [], [], DigestSkipped.None));

    private static IDigestSummarizer Failing() =>
        new FakeDigestSummarizer(_ => throw new InvalidOperationException("model is down"));

    [Fact]
    public async Task Takes_one_snapshot_of_the_effective_options_and_uses_its_digest_size()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2), Post("b", score: 8, hoursAgo: 2));
        var effective = new FakeEffectiveOptions(new AgentOptions { DigestSize = 1 });

        await new DigestJob(repo, WithOverview("О"), _notifier, _chatState, Chats, effective, new FakeTimeProvider(Now), NullLogger<DigestJob>.Instance)
            .RunAsync(CancellationToken.None);

        Assert.Single(Assert.Single(repo.SentMarks).Posts);
        Assert.Equal(1, effective.Reads);
    }

    [Fact]
    public async Task Sends_only_the_digest_with_no_rating_message_after_it()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await CreateJob(repo, WithOverview("О")).RunAsync(CancellationToken.None);

        Assert.Single(_notifier.Sent);
        Assert.All(_notifier.Keyboards, k => Assert.Empty(k.Rows));
    }

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

        var message = _notifier.Sent[0];
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

        var message = _notifier.Sent[0];
        Assert.Contains("Можна пропустити", message);
        Assert.Contains("повторів — 1", message);
    }

    [Fact]
    public async Task Still_sends_the_digest_without_the_overview_when_the_model_fails()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await CreateJob(repo, Failing()).RunAsync(CancellationToken.None);

        var message = _notifier.Sent[0];
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

    [Fact]
    public async Task Starts_a_new_conversation_with_a_snapshot_of_the_digest_after_the_posts_are_marked_sent()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));
        var store = new OrderRecordingStore(() => repo.SentMarks.Count);

        await new DigestJob(repo, WithOverview("Огляд дня"), _notifier, store, Chats,
            new FakeEffectiveOptions(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DigestJob>.Instance)
            .RunAsync(CancellationToken.None);

        var report = Assert.Single(store.Started);
        Assert.Equal("Огляд дня", report.Overview);
        Assert.Equal("Headlines", Assert.Single(report.Entries).Section);
        Assert.Equal(Now.ToString("yyyyMMddHHmmss"), report.ReportId);
        Assert.Equal(1, store.MarksWhenStarted);
    }

    [Fact]
    public async Task The_digest_still_succeeds_when_the_snapshot_cannot_be_saved()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));
        var store = new OrderRecordingStore(() => 0) { Fail = true };

        await new DigestJob(repo, WithOverview("О"), _notifier, store, Chats,
            new FakeEffectiveOptions(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DigestJob>.Instance)
            .RunAsync(CancellationToken.None);

        Assert.Single(_notifier.Sent);
        Assert.Single(repo.SentMarks);
    }

    [Fact]
    public async Task Does_not_touch_the_conversation_when_there_were_no_posts()
    {
        await _chatState.StartNewAsync(TestData.ChatId, new ReportSnapshot("old", Now, null, [], []), CancellationToken.None);

        await CreateJob(new InMemoryPostRepository(), WithOverview("О")).RunAsync(CancellationToken.None);

        Assert.Equal("old", _chatState.Current?.Report.ReportId);
    }

    [Fact]
    public async Task The_snapshot_is_made_even_when_the_model_failed_and_the_digest_was_plain()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));

        await CreateJob(repo, Failing()).RunAsync(CancellationToken.None);

        Assert.NotNull(_chatState.Current);
        Assert.Single(_chatState.Current.Report.Entries);
    }

    private sealed class OrderRecordingStore(Func<int> marks) : IChatStateStore
    {
        public List<ReportSnapshot> Started { get; } = [];
        public int MarksWhenStarted { get; private set; }
        public bool Fail { get; init; }

        public Task StartNewAsync(long chatId, ReportSnapshot report, CancellationToken ct)
        {
            if (Fail)
            {
                throw new InvalidOperationException("blob is down");
            }

            MarksWhenStarted = marks();
            Started.Add(report);
            return Task.CompletedTask;
        }

        public Task<bool> TryStartAsync(long chatId, ReportSnapshot report, CancellationToken ct) => throw new NotSupportedException();

        public Task<ChatStateHandle?> LoadAsync(long chatId, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> TrySaveAsync(long chatId, ChatStateHandle current, ChatState updated, CancellationToken ct) => throw new NotSupportedException();

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Starts_a_conversation_in_every_bound_chat()
    {
        var repo = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 2));
        var chats = new PostAgent.Shared.Notifications.ChatResolver(
            Microsoft.Extensions.Options.Options.Create(new AgentOptions()), new InMemoryChatBindingStore(42, 43));

        await new DigestJob(repo, WithOverview("О"), _notifier, _chatState, chats, new FakeEffectiveOptions(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DigestJob>.Instance)
            .RunAsync(CancellationToken.None);

        Assert.NotNull(_chatState.Of(42));
        Assert.Equal(_chatState.Of(42)!.Report.ReportId, _chatState.Of(43)!.Report.ReportId);
    }
}
