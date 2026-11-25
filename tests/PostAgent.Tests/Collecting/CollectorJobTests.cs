using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Collecting;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Collecting;

public class CollectorJobTests
{
    private readonly InMemoryPostRepository _repo = new();
    private FakeScoringQueue _queue = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeCollectionLock _lock = new();

    private CollectorJob CreateJob(string sources, FakeSourceReader reader, FakeSourceCursors cursors)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AgentOptions { Sources = sources, ScoringBatchSize = 2 });
        var collector = new SourceCollector(reader, _repo, cursors, _queue, options, NullLogger<SourceCollector>.Instance);
        return new(collector, _lock, _notifier, options, NullLogger<CollectorJob>.Instance);
    }

    private static Func<int, SourceBatch> Channel(string username, params int[] ids) =>
        _ => new SourceBatch(new SourceInfo(username, SourceType.Channel), ids.Select(i => Message(i)).ToList(), ids.Max());

    [Fact]
    public async Task Skips_the_run_while_another_collection_holds_the_telegram_session()
    {
        _lock.Busy = true;
        var reader = new FakeSourceReader(new() { ["news"] = Channel("news", 1) });
        var cursors = new FakeSourceCursors();

        await CreateJob("news", reader, cursors).RunAsync(CancellationToken.None);

        Assert.Empty(reader.Reads);
        Assert.Empty(cursors.Values);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Releases_the_session_lock_after_the_run()
    {
        var reader = new FakeSourceReader(new() { ["news"] = Channel("news", 1) });

        await CreateJob("news", reader, new FakeSourceCursors()).RunAsync(CancellationToken.None);

        Assert.Equal(1, _lock.Acquired);
        Assert.Equal(1, _lock.Released);
    }

    [Fact]
    public async Task Stores_the_posts_queues_them_for_scoring_and_advances_the_cursor()
    {
        var reader = new FakeSourceReader(new() { ["news"] = Channel("news", 1, 2, 3) });
        var cursors = new FakeSourceCursors(new() { ["news"] = 0 });

        await CreateJob("news", reader, cursors).RunAsync(CancellationToken.None);

        Assert.Equal(["news_1", "news_2", "news_3"], _repo.Added.Select(p => p.RowKey));
        Assert.Equal(_repo.Added.Select(p => p.ToRef()), _queue.Enqueued);
        Assert.Equal(2, _queue.BatchSize);
        Assert.Equal(3, cursors.Values["news"]);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Reads_from_the_saved_cursor()
    {
        var reader = new FakeSourceReader(new() { ["news"] = Channel("news", 8) });
        var cursors = new FakeSourceCursors(new() { ["news"] = 7 });

        await CreateJob("news", reader, cursors).RunAsync(CancellationToken.None);

        Assert.Equal([("news", 7)], reader.Reads);
    }

    [Fact]
    public async Task A_failing_source_does_not_stop_the_others_and_is_reported()
    {
        var reader = new FakeSourceReader(new()
        {
            ["bad"] = _ => throw new InvalidOperationException("boom"),
            ["good"] = Channel("good", 1),
        });
        var cursors = new FakeSourceCursors();

        await CreateJob("bad,good", reader, cursors).RunAsync(CancellationToken.None);

        Assert.Equal(1, cursors.Values["good"]);
        Assert.DoesNotContain("bad", cursors.Values.Keys);
        var report = Assert.Single(_notifier.Sent);
        Assert.Contains("@bad: boom", report);
    }

    [Fact]
    public async Task A_throttled_source_is_skipped_quietly_until_the_next_run()
    {
        var reader = new FakeSourceReader(new()
        {
            ["slow"] = _ => throw new SourceThrottledException("FLOOD_WAIT_300", new InvalidOperationException()),
        });
        var cursors = new FakeSourceCursors();

        await CreateJob("slow", reader, cursors).RunAsync(CancellationToken.None);

        Assert.Empty(cursors.Values);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Does_not_move_the_cursor_when_queueing_fails_so_the_next_run_reads_the_same_messages()
    {
        _queue = new FakeScoringQueue { Failure = new InvalidOperationException("queue down") };
        var reader = new FakeSourceReader(new() { ["news"] = Channel("news", 1, 2) });
        var cursors = new FakeSourceCursors(new() { ["news"] = 0 });

        await CreateJob("news", reader, cursors).RunAsync(CancellationToken.None);

        Assert.Equal(0, cursors.Values["news"]);
        Assert.Contains("@news: queue down", Assert.Single(_notifier.Sent));
    }
}
