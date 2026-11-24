using Microsoft.Azure.Functions.Worker;

namespace PostAgent.Cleanup;

public sealed class CleanupFunction(CleanupJob job)
{
    [Function(nameof(CleanupFunction))]
    public Task Run([TimerTrigger("%CleanupSchedule%")] TimerInfo timer, CancellationToken ct) => job.RunAsync(ct);
}
