using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.DailySummary;
using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.DailySummary;

public class DailySummaryJobTests
{
    // Now is 2026-11-24 08:00 UTC: today is the 24th and yesterday the 23rd.
    private readonly InMemoryDailyStatsStore _store = new();
    private readonly FakeCleanupRequestQueue _queue = new();

    private DailySummaryJob Create(InMemoryPostRepository repo, FakeCleanupRequestQueue? queue = null) =>
        new(repo, _store, queue ?? _queue, new FakeEffectiveOptions(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DailySummaryJob>.Instance);

    [Fact]
    public async Task Writes_a_summary_for_every_finished_day_in_posts_and_not_for_today()
    {
        var repo = new InMemoryPostRepository(Post("today", hoursAgo: 1), Post("yesterday", hoursAgo: 12), Post("before", hoursAgo: 36));

        await Create(repo).RunAsync(CancellationToken.None);

        Assert.Equal(["2026-11-22", "2026-11-23"], _store.Days.Keys.Order());
        Assert.Equal(1, _store.Days["2026-11-23"].Collected);
    }

    [Fact]
    public async Task Also_summarizes_days_older_than_the_retention_that_a_held_back_cleanup_left_behind()
    {
        var repo = new InMemoryPostRepository(Post("old", hoursAgo: 24 * 6));

        await Create(repo).RunAsync(CancellationToken.None);

        Assert.Equal(["2026-11-18"], _store.Days.Keys);
    }

    [Fact]
    public async Task Tells_the_cleanup_through_which_day_the_summaries_are_written_only_after_writing_them()
    {
        var repo = new InMemoryPostRepository(Post("yesterday", hoursAgo: 12));

        await Create(repo).RunAsync(CancellationToken.None);

        Assert.Equal(CleanupRequest.For(new DateOnly(2026, 11, 23)), Assert.Single(_queue.Enqueued));
        Assert.Contains("2026-11-23", _store.Days.Keys);
    }

    [Fact]
    public async Task Asks_for_the_cleanup_even_when_there_are_no_posts()
    {
        await Create(new InMemoryPostRepository()).RunAsync(CancellationToken.None);

        Assert.Single(_queue.Enqueued);
    }

    [Fact]
    public async Task A_failing_store_fails_the_job_and_asks_for_no_cleanup()
    {
        var repo = new InMemoryPostRepository(Post("yesterday", hoursAgo: 12));
        var job = new DailySummaryJob(repo, new ThrowingStore(), _queue, new FakeEffectiveOptions(new AgentOptions()), new FakeTimeProvider(Now), NullLogger<DailySummaryJob>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync(CancellationToken.None));

        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task A_smaller_summary_never_replaces_a_fuller_one()
    {
        _store.Seed(new DailyStats("2026-11-23", 10, 10, 2, 0, 5, [], [], []));

        await Create(new InMemoryPostRepository(Post("only", hoursAgo: 12))).RunAsync(CancellationToken.None);

        Assert.Equal(10, _store.Days["2026-11-23"].Collected);
    }

    [Fact]
    public async Task A_later_run_rewrites_the_day_with_the_posts_scored_since()
    {
        var repo = new InMemoryPostRepository(Unscored("late"));
        repo.Posts[0].PartitionKey = "2026-11-23";
        await Create(repo).RunAsync(CancellationToken.None);
        Assert.Equal(0, _store.Days["2026-11-23"].Scored);

        repo.Posts[0].Status = PostStatus.Scored;
        repo.Posts[0].Score = 9;
        await Create(repo).RunAsync(CancellationToken.None);

        Assert.Equal(1, _store.Days["2026-11-23"].Scored);
    }

    private sealed class ThrowingStore : IDailyStatsStore
    {
        public Task SaveAsync(DailyStats stats, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task<IReadOnlyList<DailyStats>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct) => Task.FromResult<IReadOnlyList<DailyStats>>([]);

        public Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken ct) => Task.FromResult(0);
    }
}
