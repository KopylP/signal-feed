using Microsoft.Azure.Functions.Worker;

namespace PostAgent.Collecting;

public sealed class CollectorFunction(CollectorJob job)
{
    [Function(nameof(CollectorFunction))]
    public Task Run([TimerTrigger("%CollectorSchedule%")] TimerInfo timer, CancellationToken ct) => job.RunAsync(ct);
}
