using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;

namespace PostAgent.Chat;

// Answers one queued question. The order is: ask the model, save the conversation, and only then send the answer, so that a failed
// save can never lead to a repeated send (a repeat after a timeout duplicates a delivered Telegram message).
public sealed class ChatProcessor(
    IChatStateStore store,
    IChatAgent agent,
    ITelegramNotifier notifier,
    TimeProvider time,
    ILogger<ChatProcessor> log)
{
    // Another turn or a new digest can write while the model thinks. Retrying in this process is quick, a queue redelivery takes minutes.
    private const int MaxAttempts = 3;

    public async Task HandleAsync(ChatRequest request, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var current = await store.LoadAsync(ct);
            if (current is null || current.State.Report.ReportId != request.ReportId)
            {
                log.LogInformation("Chat question dropped: its digest is gone or was replaced");
                return;
            }

            if (current.State.ProcessedUpdateIds.Contains(request.UpdateId))
            {
                log.LogInformation("Chat question {UpdateId} was already answered", request.UpdateId);
                return;
            }

            ChatReply reply;
            try
            {
                reply = await agent.AskAsync(current.State, request.Text, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "The chat could not answer question {UpdateId}", request.UpdateId);
                await TrySendAsync(ChatTexts.Failed, ct);
                return;
            }

            if (await store.TrySaveAsync(current, current.State.WithProcessed(request.UpdateId, reply.Session), ct))
            {
                await SendAsync(reply.Text, ct);
                return;
            }

            log.LogInformation("The conversation changed while answering {UpdateId}, attempt {Attempt}", request.UpdateId, attempt);
        }

        log.LogWarning("Chat question {UpdateId} dropped: the conversation kept changing", request.UpdateId);
    }

    private async Task SendAsync(string text, CancellationToken ct)
    {
        foreach (var part in ChatReplyFormatter.Split(text))
        {
            if (!await TrySendAsync(part, ct))
            {
                return;
            }
        }
    }

    // Never repeated and never thrown: the answer is already saved, and the reader can ask again.
    private async Task<bool> TrySendAsync(string html, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        try
        {
            await notifier.SendAsync(html, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "A chat answer of {Length} characters was not sent after {ElapsedMs} ms",
                html.Length, (long)time.GetElapsedTime(started).TotalMilliseconds);
            return false;
        }
    }
}
