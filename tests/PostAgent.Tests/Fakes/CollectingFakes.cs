using PostAgent.Collecting;
using PostAgent.Shared.Posts;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Tests.Fakes;

// Читач джерел: для кожного username задається результат або помилка.
internal sealed class FakeSourceReader(Dictionary<string, Func<int, SourceBatch>> sources) : ISourceReader
{
    public List<(string Username, int LastPostId)> Reads { get; } = [];

    public Task<SourceBatch> ReadAsync(string username, int lastPostId, CancellationToken ct)
    {
        Reads.Add((username, lastPostId));
        return Task.FromResult(sources[username](lastPostId));
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeSourceCursors(Dictionary<string, int>? initial = null) : ISourceCursors
{
    public Dictionary<string, int> Values { get; } = initial ?? [];

    public Task<int> GetLastPostIdAsync(string username, CancellationToken ct) =>
        Task.FromResult(Values.GetValueOrDefault(username));

    public Task SetLastPostIdAsync(string username, int lastPostId, CancellationToken ct)
    {
        Values[username] = lastPostId;
        return Task.CompletedTask;
    }

    public Task DeleteExceptAsync(IReadOnlyCollection<string> keep, CancellationToken ct)
    {
        foreach (var name in Values.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            Values.Remove(name);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakePostQueue : IPostQueue
{
    public List<PostRef> Enqueued { get; } = [];
    public int? BatchSize { get; private set; }
    public Exception? Failure { get; set; }

    public Task EnqueueAsync(IReadOnlyList<PostRef> refs, int batchSize, CancellationToken ct)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Enqueued.AddRange(refs);
        BatchSize = batchSize;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSessionLock : ISessionLock
{
    public bool Busy { get; set; }
    public int Acquired { get; private set; }
    public int Released { get; private set; }

    public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct)
    {
        if (Busy)
        {
            return Task.FromResult<IAsyncDisposable?>(null);
        }

        Acquired++;
        return Task.FromResult<IAsyncDisposable?>(new Handle(this));
    }

    private sealed class Handle(FakeSessionLock owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Released++;
            return ValueTask.CompletedTask;
        }
    }
}
