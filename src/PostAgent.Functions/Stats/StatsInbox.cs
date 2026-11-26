using System.Globalization;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Stats;

// /stats [days]: counts, the best and weakest sources, topics and the tokens the models used. No model call, so it answers at once.
// Days still in Posts that no summary covers yet (today, or a day before the first cleanup) are summarized on the spot.
public sealed class StatsInbox(
    ChatResolver resolver,
    IDailyStatsStore store,
    IUsageStore usage,
    IPostRepository posts,
    ITelegramNotifier notifier,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<StatsInbox> log) : IIncomingMessageHandler
{
    private const int DefaultDays = 7;

    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message || Arguments(text) is not { } arguments)
        {
            return false;
        }

        if (message.Chat.Type != ChatType.Private || !await resolver.IsBoundAsync(message.Chat.Id, ct))
        {
            return false;
        }

        var settings = await options.GetAsync(ct);
        if (!TryDays(arguments, settings.StatsRetentionDays, out var days))
        {
            await notifier.SendToAsync(message.Chat.Id, StatsMessage.Usage, ct);
            return true;
        }

        try
        {
            var report = await BuildAsync(days, settings, ct);
            await notifier.SendToAsync(message.Chat.Id, report.Days == 0 ? StatsMessage.Empty(days) : StatsMessage.Format(report, days), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "The statistics could not be built");
            await notifier.SendToAsync(message.Chat.Id, StatsTexts.Failed, ct);
        }

        return true;
    }

    private async Task<StatsReport> BuildAsync(int days, AgentOptions settings, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var from = today.AddDays(1 - days);

        var stored = (await store.LoadAsync(from, today, ct)).ToDictionary(d => d.Day, StringComparer.Ordinal);
        var since = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        foreach (var group in (await posts.GetSinceAsync(since, now, ct)).GroupBy(p => p.PartitionKey))
        {
            // A stored day is complete; a day in Posts without one is summarized now (today, mostly).
            if (!stored.ContainsKey(group.Key) && DailyStatsBuilder.Build(group.Key, group, settings.InterestingThreshold) is { } live)
            {
                stored[group.Key] = live;
            }
        }

        return StatsReport.Build(stored.Values.OrderBy(d => d.Day, StringComparer.Ordinal).ToList(), await usage.LoadAsync(today.AddDays(1 - Math.Min(days, UsageLog.KeepDays)), today, ct));
    }

    private static bool TryDays(string arguments, int max, out int days)
    {
        days = DefaultDays;
        var text = arguments.Trim();
        return text.Length == 0 || (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out days) && days >= 1 && days <= max);
    }

    // The text after "/stats" (also "/stats@botname"), or null when the message is another command or text.
    private static string? Arguments(string text)
    {
        var parts = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts[0].Split('@', 2)[0].Equals("/stats", StringComparison.OrdinalIgnoreCase)
            ? parts.Length > 1 ? parts[1] : string.Empty
            : null;
    }
}
