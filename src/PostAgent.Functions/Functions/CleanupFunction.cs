using Microsoft.Azure.Functions.Worker;
using PostAgent.Services;

namespace PostAgent.Functions;

/// <summary>Раз на день виконує всі ICleanupTask. Збій однієї задачі не блокує інші, але запуск позначається невдалим.</summary>
public sealed class CleanupFunction(
    IEnumerable<ICleanupTask> tasks,
    TimeProvider time,
    ILogger<CleanupFunction> log)
{
    [Function(nameof(CleanupFunction))]
    public async Task Run([TimerTrigger("%CleanupSchedule%")] TimerInfo timer, CancellationToken ct)
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
