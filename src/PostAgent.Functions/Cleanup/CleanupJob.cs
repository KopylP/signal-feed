using PostAgent.Shared.Jobs;

namespace PostAgent.Cleanup;

public sealed class CleanupJob(
    IEnumerable<ICleanupTask> tasks,
    TimeProvider time,
    ILogger<CleanupJob> log) : IJob
{
    public string Name => "cleanup";

    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var failures = new List<Exception>();

        foreach (var task in tasks)
        {
            try
            {
                log.LogInformation("Cleanup {Task}: removed {Count} records", task.Name, await task.RunAsync(now, ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Cleanup {Task} failed", task.Name);
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Some cleanup tasks failed.", failures);
        }
    }
}
