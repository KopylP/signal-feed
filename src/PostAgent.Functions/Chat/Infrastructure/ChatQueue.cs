using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Chat.Infrastructure;

public sealed class ChatQueue([FromKeyedServices(StorageNames.ChatQueue)] QueueClient queue) : IChatQueue
{
    // A question that waited longer than this is stale: answering it hours later would only confuse the reader.
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(1);

    public Task EnqueueAsync(ChatRequest request, CancellationToken ct) =>
        queue.SendMessageAsync(request.ToMessage(), timeToLive: TimeToLive, cancellationToken: ct);
}
