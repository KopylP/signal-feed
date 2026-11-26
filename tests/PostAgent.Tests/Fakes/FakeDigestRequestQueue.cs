using PostAgent.Digest;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeDigestRequestQueue : IDigestRequestQueue
{
    public List<DigestRequest> Enqueued { get; } = [];

    public bool Fail { get; init; }

    public Task EnqueueAsync(DigestRequest request, CancellationToken ct)
    {
        if (Fail)
        {
            throw new InvalidOperationException("queue is down");
        }

        Enqueued.Add(request);
        return Task.CompletedTask;
    }
}
