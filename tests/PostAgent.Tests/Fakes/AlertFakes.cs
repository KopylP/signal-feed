using PostAgent.Alerts;
using PostAgent.Shared.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Topics;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeAlertQueue : IAlertQueue
{
    public List<AlertRequest> Enqueued { get; } = [];

    public Task EnqueueAsync(AlertRequest request, CancellationToken ct)
    {
        Enqueued.Add(request);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAlertDecisionLock : IAlertDecisionLock
{
    public int Acquired { get; private set; }
    public int Released { get; private set; }

    public Task<IAsyncDisposable> AcquireAsync(CancellationToken ct)
    {
        Acquired++;
        return Task.FromResult<IAsyncDisposable>(new Handle(this));
    }

    private sealed class Handle(FakeAlertDecisionLock owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Released++;
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class InMemoryFollowedTopicStore : IFollowedTopicStore
{
    public Dictionary<string, float[]> Topics { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception? ReadFailure { get; set; }
    public int Saves { get; private set; }

    public Task<IReadOnlyDictionary<string, float[]>> LoadAsync(CancellationToken ct) =>
        ReadFailure is not null ? throw ReadFailure : Task.FromResult<IReadOnlyDictionary<string, float[]>>(new Dictionary<string, float[]>(Topics, StringComparer.OrdinalIgnoreCase));

    public Task SaveAsync(string phrase, float[] vector, CancellationToken ct)
    {
        Saves++;
        Topics[phrase] = vector;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAlertDecider : IAlertDecider
{
    public List<IReadOnlyList<PostEntity>> Decisions { get; } = [];
    public List<(string Phrase, float[] Vector)> TopicDecisions { get; } = [];
    public Exception? Failure { get; set; }

    public Task DecideAsync(IReadOnlyList<PostEntity> scored, CancellationToken ct)
    {
        Decisions.Add(scored);
        return Failure is not null ? throw Failure : Task.CompletedTask;
    }

    public Task DecideTopicAsync(string phrase, float[] vector, CancellationToken ct)
    {
        TopicDecisions.Add((phrase, vector));
        return Task.CompletedTask;
    }
}
