using PostAgent.Chat;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeChatQueue : IChatQueue
{
    public List<ChatRequest> Enqueued { get; } = [];

    public bool Fail { get; init; }

    public Task EnqueueAsync(ChatRequest request, CancellationToken ct)
    {
        if (Fail)
        {
            throw new InvalidOperationException("queue is down");
        }

        Enqueued.Add(request);
        return Task.CompletedTask;
    }
}
