using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Login.Infrastructure;

public sealed class LoginQueue([FromKeyedServices(StorageNames.LoginQueue)] QueueClient queue) : ILoginQueue
{
    // The attempt expires in 5 minutes; a message that waited longer has nothing left to do.
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(10);

    public Task EnqueueAsync(LoginRequest request, CancellationToken ct) =>
        queue.SendMessageAsync(request.ToMessage(), timeToLive: TimeToLive, cancellationToken: ct);
}
