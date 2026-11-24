using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Settings;

namespace PostAgent.RunNow;

// Starts a job on demand: the message text is a job name, for example "digest" (tools/run-now.cs puts it
// into the queue). Access is controlled by Azure RBAC on the queue and by the ManualRunsEnabled setting.
public sealed class RunNowFunction(
    IEnumerable<IJob> jobs,
    IOptions<AgentOptions> options,
    ILogger<RunNowFunction> log)
{
    [Function(nameof(RunNowFunction))]
    public async Task Run(
        [QueueTrigger(StorageNames.RunNowQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct)
    {
        if (!options.Value.ManualRunsEnabled)
        {
            log.LogWarning("Run-now message dropped: ManualRunsEnabled is off");
            return;
        }

        var name = message.Trim();
        var job = jobs.FirstOrDefault(j => j.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            log.LogWarning("Run-now message dropped, unknown job. Known jobs: {Jobs}", string.Join(", ", jobs.Select(j => j.Name)));
            return;
        }

        log.LogInformation("Run-now: starting {Job}", job.Name);
        try
        {
            await job.RunAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not rethrown: a retry would send the digest or move the cursors again. The timer does not retry either.
            log.LogError(ex, "Run-now: {Job} failed", job.Name);
        }
    }
}
