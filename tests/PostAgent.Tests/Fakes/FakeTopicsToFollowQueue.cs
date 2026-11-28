using PostAgent.Shared.Topics;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeTopicsToFollowQueue : ITopicsToFollowQueue
{
    public List<string> Enqueued { get; } = [];
    public Exception? Failure { get; set; }

    public Task EnqueueAsync(string phrase, CancellationToken ct)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Enqueued.Add(phrase);
        return Task.CompletedTask;
    }
}
