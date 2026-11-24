using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using static PostAgent.Tests.TestData;
using PostAgent.Collecting;
using PostAgent.Posts;
using PostAgent.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Collecting;

public class CollectorFunctionTests
{
    private readonly InMemoryPostRepository _repo = new();
    private readonly FakeScoringQueue _queue = new();
    private readonly RecordingNotifier _notifier = new();

    private CollectorFunction CreateFunction(string sources, FakeSourceReader reader, FakeSourceCursors cursors) =>
        new(reader, _repo, cursors, _queue, _notifier,
            Microsoft.Extensions.Options.Options.Create(new AgentOptions { Sources = sources, ScoringBatchSize = 2 }),
            NullLogger<CollectorFunction>.Instance);

    private static Func<int, SourceBatch> Channel(string username, params int[] ids) =>
        _ => new SourceBatch(new SourceInfo(username, SourceType.Channel), ids.Select(i => Message(i)).ToList(), ids.Max());

    [Fact]
    public async Task Stores_the_posts_queues_them_for_scoring_and_advances_the_cursor()
    {
        var reader = new FakeSourceReader(new() { ["news"] = Channel("news", 1, 2, 3) });
        var cursors = new FakeSourceCursors(new() { ["news"] = 0 });

        await CreateFunction("news", reader, cursors).Run(new TimerInfo(), CancellationToken.None);

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

        await CreateFunction("news", reader, cursors).Run(new TimerInfo(), CancellationToken.None);

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

        await CreateFunction("bad,good", reader, cursors).Run(new TimerInfo(), CancellationToken.None);

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

        await CreateFunction("slow", reader, cursors).Run(new TimerInfo(), CancellationToken.None);

        Assert.Empty(cursors.Values);
        Assert.Empty(_notifier.Sent);
    }
}
