using PostAgent.Shared.Common;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Collecting;

public sealed class CollectorJob(
    SourceCollector collector,
    ICollectionLock collectionLock,
    ITelegramNotifier notifier,
    IEffectiveOptions options,
    SettingsEditor editor,
    ISourceCursors cursors,
    ILogger<CollectorJob> log) : IJob
{
    private const int MinPauseMs = 1000;
    private const int MaxPauseMs = 2000;
    private const int MaxErrorChars = 150;

    public string Name => "collect";

    public async Task RunAsync(CancellationToken ct)
    {
        await using var held = await collectionLock.TryAcquireAsync(ct);
        if (held is null)
        {
            log.LogWarning("Collection skipped: another collection is using the Telegram session");
            return;
        }

        var settings = await options.GetAsync(ct);
        await TryForgetRemovedSourcesAsync(settings, ct);

        var failures = new List<(string Username, string Line)>();
        foreach (var (index, username) in settings.SourceList.Index())
        {
            if (index > 0)
            {
                await Task.Delay(Random.Shared.Next(MinPauseMs, MaxPauseMs), ct); // do not overload Telegram
            }

            if (await TryCollectAsync(username, settings, ct) is { } failure)
            {
                failures.Add((username, failure));
            }
        }

        // Without this report a failure (for example an expired session) would look like a quiet day.
        if (failures.Count > 0)
        {
            var unverified = await TryGetUnverifiedAsync(ct);
            await notifier.SendAsync(
                CollectionFailureMessage.Format(failures.Select(f => f.Line).ToList()),
                CollectionFailureMessage.RemovalKeyboard(failures.Select(f => f.Username).Where(unverified.Contains)),
                ct);
        }
    }

    private async Task<string?> TryCollectAsync(string username, AgentOptions settings, CancellationToken ct)
    {
        try
        {
            await collector.CollectAsync(username, settings, ct);
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

    // A source removed in the bot leaves its cursor behind; a later re-add must start as a new source, not replay the gap.
    private async Task TryForgetRemovedSourcesAsync(AgentOptions settings, CancellationToken ct)
    {
        try
        {
            await cursors.DeleteExceptAsync(settings.SourceList, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Cursors of removed sources could not be cleaned up");
        }
    }

    private async Task<IReadOnlyList<string>> TryGetUnverifiedAsync(CancellationToken ct)
    {
        try
        {
            return await editor.UnverifiedSourcesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Unverified sources could not be listed");
            return [];
        }
    }
}
