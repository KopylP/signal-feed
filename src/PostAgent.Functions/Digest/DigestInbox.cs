using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Digest;

// /digest [period]: only checks and queues, because the digest takes a model call and the webhook must answer Telegram fast.
public sealed class DigestInbox(
    ChatResolver resolver,
    IDigestRequestQueue queue,
    ITelegramNotifier notifier,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<DigestInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message || Command(text) is not { } arguments)
        {
            return false;
        }

        if (message.Chat.Type != ChatType.Private || !await resolver.IsBoundAsync(message.Chat.Id, ct))
        {
            return false;
        }

        var settings = await options.GetAsync(ct);
        var zone = LocalTime.Zone(settings.TimeZone);
        if (DigestPeriod.Parse(arguments, time.GetUtcNow(), zone, settings.RetentionDays) is not { } period)
        {
            await notifier.SendToAsync(message.Chat.Id, DigestTexts.Usage, ct);
            return true;
        }

        try
        {
            await queue.EnqueueAsync(new DigestRequest(message.Chat.Id, period.Since, period.Until), ct);
            await notifier.SendToAsync(message.Chat.Id, DigestTexts.Preparing(period.Describe(zone)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Telegram resends an update on a 5xx only for a while; the reader can just ask again.
            log.LogError(ex, "A digest request could not be queued");
        }

        return true;
    }

    // The text after "/digest" (also "/digest@botname"), or null when the message is another command or text.
    private static string? Command(string text)
    {
        var parts = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts[0].Split('@', 2)[0].Equals("/digest", StringComparison.OrdinalIgnoreCase)
            ? parts.Length > 1 ? parts[1] : string.Empty
            : null;
    }
}
