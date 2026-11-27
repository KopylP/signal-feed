using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types;

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
        if (update.Message is not { Text: { } text } message || BotCommandText.Parse(text) is not { Name: "/digest", Arguments: var arguments })
        {
            return false;
        }

        if (!await resolver.ServesAsync(message.Chat, ct))
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
}
