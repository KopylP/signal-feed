using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Collecting;

public sealed class CollectorFunction(
    SourceCollector collector,
    ITelegramNotifier notifier,
    IOptions<AgentOptions> options,
    ILogger<CollectorFunction> log)
{
    private const int MinPauseMs = 1000;
    private const int MaxPauseMs = 2000;
    private const int MaxErrorChars = 150;

    [Function(nameof(CollectorFunction))]
    public async Task Run([TimerTrigger("%CollectorSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var failures = new List<string>();
        foreach (var (index, username) in options.Value.SourceList.Index())
        {
            if (index > 0)
            {
                await Task.Delay(Random.Shared.Next(MinPauseMs, MaxPauseMs), ct); // do not overload Telegram
            }

            if (await TryCollectAsync(username, ct) is { } failure)
            {
                failures.Add(failure);
            }
        }

        // Without this report a failure (for example an expired session) would look like a quiet day.
        if (failures.Count > 0)
        {
            await notifier.SendAsync(CollectionFailureMessage.Format(failures), ct);
        }
    }

    private async Task<string?> TryCollectAsync(string username, CancellationToken ct)
    {
        try
        {
            await collector.CollectAsync(username, ct);
            return null;
        }
        catch (SourceThrottledException ex)
        {
            log.LogWarning("@{Source} skipped until the next run: {Message}", username, ex.Message);
            return null; // an expected temporary skip, not a failure
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Collecting @{Source} failed", username);
            return $"@{username}: {ex.Message.Truncate(MaxErrorChars, "…")}";
        }
    }
}
