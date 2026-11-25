namespace PostAgent.Chat.Agent;

// The preview of one answer. It never fails the answer: a draft is a nicety, so the first error ends the stream and the answer
// arrives as one message, as before. Updates are spaced out, because Telegram limits how often a bot may send.
public sealed class ChatDraftStream(IChatDrafts drafts, TimeProvider time, ILogger<ChatDraftStream> log, long chatId, long draftId)
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(700);

    private string _last = string.Empty;
    private long _lastSentAt;
    private bool _started;
    private bool _failed;

    // An empty draft: Telegram shows "Thinking..." while the model reasons or calls a tool.
    public Task StartAsync(CancellationToken ct) => SendAsync(string.Empty, ct);

    // The whole text so far, not the new part. Sent when it changed and enough time has passed since the last send.
    public Task OnTextAsync(string textSoFar, CancellationToken ct)
    {
        if (_failed || textSoFar.Length == 0 || textSoFar == _last || time.GetElapsedTime(_lastSentAt) < MinInterval)
        {
            return Task.CompletedTask;
        }

        return SendAsync(textSoFar, ct);
    }

    private async Task SendAsync(string text, CancellationToken ct)
    {
        if (_failed)
        {
            return;
        }

        try
        {
            await drafts.SendAsync(chatId, draftId, text, ct);
            _last = text;
            _lastSentAt = time.GetTimestamp();
            _started = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _failed = true;
            log.LogWarning("The answer preview stopped after {Sent} update(s): {Reason}", _started ? 1 : 0, ex.GetType().Name);
        }
    }
}
