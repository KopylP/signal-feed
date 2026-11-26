using PostAgent.Shared.Notifications;

namespace PostAgent.Chat.Answering;

// Sends what the chat has to say to one chat. A send is never repeated and never thrown: the answer is already saved, and the reader can ask again.
public sealed class ChatDelivery(ITelegramNotifier notifier, TimeProvider time, ILogger<ChatDelivery> log)
{
    // The OpenAI client and HttpClient report their own timeout as a cancellation. Only a cancelled token means the host is stopping;
    // anything else is a failure to answer once, never a reason to redeliver the question (each redelivery is another model call).
    public static bool IsFailure(Exception ex, CancellationToken ct) => ex is not OperationCanceledException || !ct.IsCancellationRequested;

    // The text is split into messages that Telegram accepts; the rest is dropped after the first one that fails.
    // The last part carries the agent's choices as buttons, or takes away the buttons of an earlier answer that the reader did not tap.
    public async Task SendAnswerAsync(long chatId, string text, IReadOnlyList<string>? choices, CancellationToken ct)
    {
        var parts = ChatReplyFormatter.Split(text);
        for (var i = 0; i < parts.Count; i++)
        {
            var keyboard = i < parts.Count - 1 ? null : choices is { Count: > 0 } ? ReplyKeyboard.Of(choices) : ReplyKeyboard.Remove;
            if (!await TrySendAsync(chatId, parts[i], keyboard, ct))
            {
                return;
            }
        }
    }

    public Task<bool> TrySendAsync(long chatId, string html, CancellationToken ct) => TrySendAsync(chatId, html, null, ct);

    private async Task<bool> TrySendAsync(long chatId, string html, ReplyKeyboard? keyboard, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        try
        {
            await (keyboard is null ? notifier.SendToAsync(chatId, html, ct) : notifier.SendToAsync(chatId, html, keyboard, ct));
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
