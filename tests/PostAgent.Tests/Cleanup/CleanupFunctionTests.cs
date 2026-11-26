using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Cleanup;
using PostAgent.Shared.Cleanup;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Cleanup;

public class CleanupFunctionTests
{
    private static (CleanupFunction Function, FakeCleanupTask Task) Create()
    {
        var task = new FakeCleanupTask("one", () => 1);
        var runner = new CleanupRunner([task], new FakeTimeProvider(Now), NullLogger<CleanupRunner>.Instance);
        return (new CleanupFunction(runner, NullLogger<CleanupFunction>.Instance), task);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"summarizedThrough\":\"yesterday\"}")]
    public async Task A_malformed_request_is_dropped_without_running_any_cleanup_and_without_failing(string message)
    {
        var (function, task) = Create();

        await function.Run(message, CancellationToken.None);

        Assert.Null(task.RanAt);
    }

    [Fact]
    public async Task A_valid_request_runs_every_cleanup_task_with_its_day()
    {
        var (function, task) = Create();
        var request = CleanupRequest.For(new DateOnly(2026, 11, 23));

        await function.Run(request.ToMessage(), CancellationToken.None);

        Assert.Equal((Now, request), (task.RanAt, task.Request));
    }
}
