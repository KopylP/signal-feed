using System.Text.Json;
using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Topics.Infrastructure;

public sealed class TopicsToFollowQueue([FromKeyedServices(StorageNames.TopicsToFollowQueue)] QueueClient queue) : ITopicsToFollowQueue
{
    public async Task EnqueueAsync(string phrase, CancellationToken ct) =>
        await queue.SendMessageAsync(JsonSerializer.Serialize(phrase), ct);

    public static string Parse(string message) => JsonSerializer.Deserialize<string>(message) ?? string.Empty;
}
