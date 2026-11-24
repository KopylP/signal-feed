using Microsoft.Azure.Functions.Worker;

namespace PostAgent.Digest;

public sealed class DigestFunction(DigestJob job)
{
    [Function(nameof(DigestFunction))]
    public Task Run([TimerTrigger("%DigestSchedule%")] TimerInfo timer, CancellationToken ct) => job.RunAsync(ct);
}
