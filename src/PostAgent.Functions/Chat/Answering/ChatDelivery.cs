using PostAgent.Shared.Notifications;

namespace PostAgent.Chat.Answering;

// Sends what the chat has to say to one chat. A send is never repeated and never thrown: the answer is already saved, and the reader can ask again.
public sealed class ChatDelivery(ITelegramNotifier notifier, TimeProvider time, ILogger<ChatDelivery> log)
{
    // The OpenAI client and HttpClient report their own timeout as a cancellation. Only a cancelled token means the host is stopping;
    // anything else is a failure to answer once, never a reason to redeliver the question (each redelivery is another model call).
    public static bool IsFailure(Exception ex, CancellationToken ct) => ex is not OperationCanceledException || !ct.IsCancellationRequested;

    // The text is split into messages that Telegram accepts; the rest is dropped after the first one that fails.
    public async Task SendAnswerAsync(long chatId, string text, CancellationToken ct)
    {
        foreach (var part in ChatReplyFormatter.Split(text))
        {
            if (!await TrySendAsync(chatId, part, ct))
            {
                return;
            }
        }
    }

    public async Task<bool> TrySendAsync(long chatId, string html, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        try
        {
            await notifier.SendToAsync(chatId, html, ct);
            return true;
        }
        catch (Exception ex) when (IsFailure(ex, ct))
        {
            log.LogError(ex, "A chat answer of {Length} characters was not sent after {ElapsedMs} ms",
                html.Length, (long)time.GetElapsedTime(started).TotalMilliseconds);
            return false;
        }
    }
}
