using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Chat;

public sealed class ChatFunction(ChatProcessor processor, ILogger<ChatFunction> log)
{
    [Function(nameof(ChatFunction))]
    public async Task Run(
        [QueueTrigger(StorageNames.ChatQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct)
    {
        var request = ChatRequest.Parse(message);
        if (request is null)
        {
            // Retrying a message that cannot be read only ends in the poison queue.
            log.LogWarning("A malformed chat queue message was dropped");
            return;
        }

        await processor.HandleAsync(request, ct);
    }
}
