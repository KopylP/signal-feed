using Microsoft.Azure.Functions.Worker;

namespace PostAgent.Alerts;

public sealed class AlertFunction(AlertJob job)
{
    [Function(nameof(AlertFunction))]
    public Task Run([TimerTrigger("%AlertSchedule%")] TimerInfo timer, CancellationToken ct) => job.RunAsync(ct);
}
