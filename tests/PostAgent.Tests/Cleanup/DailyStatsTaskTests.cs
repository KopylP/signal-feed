using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Cleanup;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Cleanup;

public class DailyStatsTaskTests
{
    // Now is 2026-11-24 08:00 UTC: today is the 24th, yesterday the 23rd, and with RetentionDays 2 the 22nd is the oldest day still in Posts.
    private readonly InMemoryDailyStatsStore _store = new();

    private DailyStatsTask Create(InMemoryPostRepository repo, Action<AgentOptions>? configure = null)
    {
        var app = new AgentOptions();
        configure?.Invoke(app);
        return new DailyStatsTask(repo, _store, new FakeEffectiveOptions(app), NullLogger<DailyStatsTask>.Instance);
    }

    [Fact]
    public async Task Writes_a_summary_for_every_finished_day_still_in_posts_and_not_for_today()
    {
        var repo = new InMemoryPostRepository(Post("today", hoursAgo: 1), Post("yesterday", hoursAgo: 12), Post("before", hoursAgo: 36));

        await Create(repo).RunAsync(Now, CancellationToken.None);

        Assert.Equal(["2026-11-22", "2026-11-23"], _store.Days.Keys.Order());
        Assert.Equal(1, _store.Days["2026-11-23"].Collected);
    }

    [Fact]
    public async Task Also_summarizes_the_day_the_posts_cleanup_deletes_in_the_same_run()
    {
        // 2026-11-21 20:00 is the partition PostsCleanup deletes at this moment (RetentionDays 2 keeps the 22nd, 23rd and 24th).
        var repo = new InMemoryPostRepository(Post("last", hoursAgo: 60));

        await Create(repo).RunAsync(Now, CancellationToken.None);

        Assert.Equal(["2026-11-21"], _store.Days.Keys);
    }

    [Fact]
    public async Task A_smaller_summary_never_replaces_a_fuller_one()
    {
        _store.Seed(new DailyStats("2026-11-23", 10, 10, 2, 0, 5, [], [], []));
        var repo = new InMemoryPostRepository(Post("only", hoursAgo: 12));

        await Create(repo).RunAsync(Now, CancellationToken.None);

        Assert.Equal(10, _store.Days["2026-11-23"].Collected);
    }

    [Fact]
    public async Task A_later_run_rewrites_the_day_with_the_posts_scored_since()
    {
        var repo = new InMemoryPostRepository(Unscored("late"));
        repo.Posts[0].PartitionKey = "2026-11-23";
        await Create(repo).RunAsync(Now, CancellationToken.None);
        Assert.Equal(0, _store.Days["2026-11-23"].Scored);

        repo.Posts[0].Status = PostAgent.Shared.Posts.PostStatus.Scored;
        repo.Posts[0].Score = 9;
        await Create(repo).RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, _store.Days["2026-11-23"].Scored);
    }

    [Fact]
    public async Task Deletes_summaries_older_than_the_kept_period_and_returns_how_many()
    {
        _store.Seed(new DailyStats("2026-08-01", 1, 1, 0, 0, 1, [], [], []), new DailyStats("2026-11-01", 1, 1, 0, 0, 1, [], [], []));

        var deleted = await Create(new InMemoryPostRepository(), o => o.StatsRetentionDays = 30).RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["2026-11-01"], _store.Days.Keys);
    }
}
