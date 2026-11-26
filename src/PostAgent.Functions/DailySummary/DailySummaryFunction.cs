using Microsoft.Azure.Functions.Worker;

namespace PostAgent.DailySummary;

public sealed class DailySummaryFunction(DailySummaryJob job)
{
    [Function(nameof(DailySummaryFunction))]
    public Task Run([TimerTrigger("%DailySummarySchedule%")] TimerInfo timer, CancellationToken ct) => job.RunAsync(ct);
}
