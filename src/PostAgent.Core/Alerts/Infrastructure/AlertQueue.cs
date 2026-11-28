using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Alerts.Infrastructure;

public sealed class AlertQueue([FromKeyedServices(StorageNames.AlertQueue)] QueueClient queue) : IAlertQueue
{
    public async Task EnqueueAsync(AlertRequest request, CancellationToken ct) =>
        await queue.SendMessageAsync(request.ToMessage(), ct);
}
