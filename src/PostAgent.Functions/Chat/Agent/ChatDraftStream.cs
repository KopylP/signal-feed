namespace PostAgent.Chat.Agent;

// The preview of one answer. It never fails the answer: a draft is a nicety, so the first error ends the stream and the answer
// arrives as one message, as before. Updates are spaced out, because Telegram limits how often a bot may send.
// A draft lives about 30 seconds, so while the model thinks without writing (reasoning, a tool call) the last text is sent again.
public sealed class ChatDraftStream(IChatDrafts drafts, TimeProvider time, ILogger<ChatDraftStream> log, long chatId, long draftId)
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(700);
    public static readonly TimeSpan KeepAliveEvery = TimeSpan.FromSeconds(20);

    // The Bot API takes 4096 characters; a longer answer shows its end, and the real message carries all of it.
    public const int MaxChars = 4000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _last = string.Empty;
    private long _lastSentAt;
    private int _sent;
    private bool _failed;

    // An empty draft: Telegram shows "Thinking..." while the model reasons or calls a tool.
    public async Task StartAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await SendLockedAsync(string.Empty, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // The whole text so far, not the new part. Sent when it changed and enough time has passed since the last send.
    public async Task OnTextAsync(string textSoFar, CancellationToken ct)
    {
        var text = textSoFar.Length > MaxChars ? textSoFar[^MaxChars..] : textSoFar;
        await _gate.WaitAsync(ct);
        try
        {
            if (_failed || text.Length == 0 || text == _last || time.GetElapsedTime(_lastSentAt) < MinInterval)
            {
                return;
            }

            await SendLockedAsync(text, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Runs next to the model call until the token is cancelled: sends the last text again when nothing was sent for a while.
    public async Task RunKeepAliveAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(KeepAliveEvery, time, ct);
                await _gate.WaitAsync(ct);
                try
                {
                    if (!_failed && _sent > 0 && time.GetElapsedTime(_lastSentAt) >= KeepAliveEvery)
                    {
                        await SendLockedAsync(_last, ct);
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The answer is done: the keep-alive ends with it.
        }
    }

    private async Task SendLockedAsync(string text, CancellationToken ct)
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
            _sent++;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A client reports its own timeout as a cancellation; only a cancelled token means the host is stopping.
            _failed = true;
            log.LogWarning("The answer preview stopped after {Sent} update(s): {Reason}", _sent, ex.GetType().Name);
        }
    }
}
