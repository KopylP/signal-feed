using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static PostAgent.Tests.TestData;
using PostAgent.Cleanup;
using PostAgent.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Cleanup;

public class CleanupFunctionTests
{
    private static CleanupFunction CreateFunction(params ICleanupTask[] tasks) =>
        new(tasks, new FakeTimeProvider(Now), NullLogger<CleanupFunction>.Instance);

    [Fact]
    public async Task Runs_every_task_with_the_current_time()
    {
        var first = new FakeCleanupTask("one", () => 3);
        var second = new FakeCleanupTask("two", () => 0);

        await CreateFunction(first, second).Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal(Now, first.RanAt);
        Assert.Equal(Now, second.RanAt);
    }

    [Fact]
    public async Task A_failing_task_does_not_block_the_others_but_fails_the_run()
    {
        var failing = new FakeCleanupTask("broken", () => throw new InvalidOperationException("boom"));
        var healthy = new FakeCleanupTask("healthy", () => 1);

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            CreateFunction(failing, healthy).Run(new TimerInfo(), CancellationToken.None));

        Assert.Single(error.InnerExceptions);
        Assert.Equal(Now, healthy.RanAt);
    }

    [Fact]
    public async Task Posts_cleanup_deletes_days_older_than_the_retention()
    {
        var repo = new InMemoryPostRepository { DeletedCount = 7 };
        var cleanup = new PostsCleanup(repo, Microsoft.Extensions.Options.Options.Create(new AgentOptions { RetentionDays = 2 }));

        var removed = await cleanup.RunAsync(Now, CancellationToken.None);

        Assert.Equal(7, removed);
        Assert.Equal(Now.AddDays(-2), repo.DeletedBefore);
    }
}
