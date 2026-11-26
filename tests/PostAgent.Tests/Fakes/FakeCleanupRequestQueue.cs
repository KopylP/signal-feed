using PostAgent.Shared.Cleanup;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeCleanupRequestQueue : ICleanupRequestQueue
{
    public List<CleanupRequest> Enqueued { get; } = [];

    public bool Fail { get; init; }

    public Task EnqueueAsync(CleanupRequest request, CancellationToken ct)
    {
        if (Fail)
        {
            throw new InvalidOperationException("queue is down");
        }

        Enqueued.Add(request);
        return Task.CompletedTask;
    }
}
