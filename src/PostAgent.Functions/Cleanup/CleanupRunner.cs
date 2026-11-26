using PostAgent.Shared.Cleanup;

namespace PostAgent.Cleanup;

// Runs every cleanup task for one request. The tasks are idempotent, so a failure fails the run and the queue retries it.
public sealed class CleanupRunner(
    IEnumerable<ICleanupTask> tasks,
    TimeProvider time,
    ILogger<CleanupRunner> log)
{
    public async Task RunAsync(CleanupRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var failures = new List<Exception>();

        foreach (var task in tasks)
        {
            try
            {
                log.LogInformation("Cleanup {Task}: removed {Count} records", task.Name, await task.RunAsync(request, now, ct));
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
