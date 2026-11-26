using PostAgent.Cleanup;
using PostAgent.Shared.Cleanup;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeCleanupTask(string name, Func<int> run) : ICleanupTask
{
    public string Name => name;
    public DateTimeOffset? RanAt { get; private set; }
    public CleanupRequest? Request { get; private set; }

    public Task<int> RunAsync(CleanupRequest request, DateTimeOffset now, CancellationToken ct)
    {
        RanAt = now;
        Request = request;
        return Task.FromResult(run());
    }
}
