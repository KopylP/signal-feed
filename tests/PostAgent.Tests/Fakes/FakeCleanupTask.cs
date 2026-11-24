using PostAgent.Cleanup;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeCleanupTask(string name, Func<int> run) : ICleanupTask
{
    public string Name => name;
    public DateTimeOffset? RanAt { get; private set; }

    public Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        RanAt = now;
        return Task.FromResult(run());
    }
}
