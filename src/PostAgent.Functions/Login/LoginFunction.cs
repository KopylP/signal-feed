using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Login;

public sealed class LoginFunction(LoginProcessor processor, ILogger<LoginFunction> log)
{
    [Function(nameof(LoginFunction))]
    public async Task Run(
        [QueueTrigger(StorageNames.LoginQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct)
    {
        var request = LoginRequest.Parse(message);
        if (request is null)
        {
            log.LogWarning("A malformed sign-in queue message was dropped");
            return;
        }

        await processor.RunAsync(request, ct);
    }
}
