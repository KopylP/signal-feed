namespace PostAgent.Chat.Agent;

// The preview of one answer. It never fails the answer: a draft is a nicety, so the first error ends the stream and the answer
// arrives as one message, as before. Updates are spaced out, because Telegram limits how often a bot may send.
// A draft lives about 30 seconds, so while the model thinks without writing (reasoning, a tool call) the last text is sent again,
// and a text that arrived inside the spacing window is sent as soon as the window is over, not lost.
public sealed class ChatDraftStream(IChatDrafts drafts, TimeProvider time, ILogger<ChatDraftStream> log, long chatId, long draftId)
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(700);
    public static readonly TimeSpan KeepAliveEvery = TimeSpan.FromSeconds(20);

    // How often the keep-alive looks; it sends when KeepAliveEvery has passed since the last send, so the gap stays well under the 30 s life of a draft.
    public static readonly TimeSpan KeepAliveCheckEvery = TimeSpan.FromSeconds(5);

    // What the preview says until there is something worth reading. The clients we tried showed nothing for an empty draft, so it says "thinking" itself.
    public const string Placeholder = "💭 Думаю";

    // A text of one or two words is not shown: it jumps and says nothing, so the placeholder stays until the third word.
    public const int MinWords = 3;

    // The Bot API takes 4096 characters; a longer answer shows its end, and the real message carries all of it.
    public const int MaxChars = 4000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _last = string.Empty;
    private string? _pending;
    private long _lastSentAt;
    private int _sent;
    private bool _failed;
    private bool _enoughWords;

    // The placeholder, while the model reasons or calls a tool.
    public async Task StartAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await SendLockedAsync(Placeholder, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // The whole text so far, not the new part. Not shown until it has a few words; then sent when it changed and enough time has passed since
    // the last send. A text that has to wait is kept, and the keep-alive sends it when the window is over.
    public async Task OnTextAsync(string textSoFar, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // Counted until the threshold is passed once, then never again: the text only grows.
            if (!_enoughWords)
            {
                _enoughWords = textSoFar.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= MinWords;
                if (!_enoughWords)
                {
                    return;
                }
            }

            var text = textSoFar.Length > MaxChars ? textSoFar[^MaxChars..] : textSoFar;
            if (_failed || text.Length == 0 || text == _last)
            {
                return;
            }

            if (time.GetElapsedTime(_lastSentAt) < MinInterval)
            {
                _pending = text;
                return;
            }

            await SendLockedAsync(text, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Runs next to the model call until the token is cancelled: sends a text that had to wait, or the last text again when nothing was sent for a while.
    public async Task RunKeepAliveAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(KeepAliveCheckEvery, time, ct);
                await _gate.WaitAsync(ct);
                try
                {
                    if (_failed || _sent == 0)
                    {
                        continue;
                    }

                    var elapsed = time.GetElapsedTime(_lastSentAt);
                    if (_pending is not null && _pending != _last && elapsed >= MinInterval)
                    {
                        await SendLockedAsync(_pending, ct);
                    }
                    else if (elapsed >= KeepAliveEvery)
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
            _pending = null;
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
