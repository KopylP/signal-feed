using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Cleanup;
using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Cleanup;

public class CleanupRunnerTests
{
    private static readonly CleanupRequest Through23 = CleanupRequest.For(new DateOnly(2026, 11, 23));

    private static CleanupRunner Create(params ICleanupTask[] tasks) =>
        new(tasks, new FakeTimeProvider(Now), NullLogger<CleanupRunner>.Instance);

    [Fact]
    public async Task Runs_every_task_with_the_request_and_the_current_time()
    {
        var first = new FakeCleanupTask("one", () => 3);
        var second = new FakeCleanupTask("two", () => 0);

        await Create(first, second).RunAsync(Through23, CancellationToken.None);

        Assert.Equal((Now, Through23), (first.RanAt, first.Request));
        Assert.Equal((Now, Through23), (second.RanAt, second.Request));
    }

    [Fact]
    public async Task A_failing_task_does_not_block_the_others_but_fails_the_run_so_the_queue_retries()
    {
        var failing = new FakeCleanupTask("broken", () => throw new InvalidOperationException("boom"));
        var healthy = new FakeCleanupTask("healthy", () => 1);

        var error = await Assert.ThrowsAsync<AggregateException>(() => Create(failing, healthy).RunAsync(Through23, CancellationToken.None));

        Assert.Single(error.InnerExceptions);
        Assert.Equal(Now, healthy.RanAt);
    }

    [Fact]
    public async Task Posts_cleanup_deletes_days_older_than_the_retention()
    {
        var repo = new InMemoryPostRepository { DeletedCount = 7 };
        var cleanup = new PostsCleanup(repo, Microsoft.Extensions.Options.Options.Create(new AgentOptions { RetentionDays = 2 }));

        var removed = await cleanup.RunAsync(Through23, Now, CancellationToken.None);

        Assert.Equal(7, removed);
        Assert.Equal(Now.AddDays(-2), repo.DeletedBefore);
    }

    [Fact]
    public async Task Posts_cleanup_never_deletes_a_day_that_has_no_summary_yet()
    {
        // The summaries reach only the 20th, so the 21st must stay even though the retention would allow deleting it.
        var repo = new InMemoryPostRepository();
        var cleanup = new PostsCleanup(repo, Microsoft.Extensions.Options.Options.Create(new AgentOptions { RetentionDays = 1 }));

        await cleanup.RunAsync(CleanupRequest.For(new DateOnly(2026, 11, 20)), Now, CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero), repo.DeletedBefore);
    }

    [Fact]
    public async Task Old_summaries_are_deleted_after_their_retention()
    {
        var store = new InMemoryDailyStatsStore();
        store.Seed(new DailyStats("2026-08-01", 1, 1, 0, 0, 1, [], [], []), new DailyStats("2026-11-01", 1, 1, 0, 0, 1, [], [], []));
        var cleanup = new OldDailySummariesCleanup(store, new FakeEffectiveOptions(new AgentOptions { StatsRetentionDays = 30 }));

        var deleted = await cleanup.RunAsync(Through23, Now, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["2026-11-01"], store.Days.Keys);
    }
}
