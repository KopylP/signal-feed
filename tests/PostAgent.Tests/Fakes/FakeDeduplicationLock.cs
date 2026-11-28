using PostAgent.Deduplication;

namespace PostAgent.Tests.Fakes;

// Counts the holds and lets a test look at the state at the moment the lock is taken and released.
internal sealed class FakeDeduplicationLock : IDeduplicationLock
{
    public int Acquired { get; private set; }
    public int Released { get; private set; }
    public Action? OnAcquire { get; set; }
    public Action? OnRelease { get; set; }

    public Task<IAsyncDisposable> AcquireAsync(CancellationToken ct)
    {
        Acquired++;
        OnAcquire?.Invoke();
        return Task.FromResult<IAsyncDisposable>(new Handle(this));
    }

    private sealed class Handle(FakeDeduplicationLock owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Released++;
            owner.OnRelease?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
