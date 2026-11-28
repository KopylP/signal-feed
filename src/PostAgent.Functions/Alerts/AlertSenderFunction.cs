using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Alerts;

public sealed class AlertSenderFunction(AlertSender sender)
{
    [Function(nameof(AlertSenderFunction))]
    public Task Run([QueueTrigger(StorageNames.AlertQueue, Connection = "AzureWebJobsStorage")] string message, CancellationToken ct) =>
        sender.SendAsync(AlertRequest.Parse(message), ct);
}
