using PostAgent.Chat.Agent;
using PostAgent.Chat.Intake;
using PostAgent.Shared.Chat;

namespace PostAgent.Chat.Answering;

// Answers one queued question. The order is: ask the model, save the conversation, and only then send the answer, so that a failed
// save can never lead to a repeated send (a repeat after a timeout duplicates a delivered Telegram message).
public sealed class ChatProcessor(
    IChatStateStore store,
    IChatAgent agent,
    ChatDelivery delivery,
    SettingsProposalSender proposals,
    ILogger<ChatProcessor> log)
{
    // Another turn or a new digest can write while the model thinks. Retrying in this process is quick, a queue redelivery takes minutes.
    private const int MaxAttempts = 3;

    public async Task HandleAsync(ChatRequest request, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var current = await store.LoadAsync(request.ChatId, ct);
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
                reply = await agent.AskAsync(request.ChatId, current.State, request.Text, request.SentAt, ct);
            }
            catch (Exception ex) when (ChatDelivery.IsFailure(ex, ct))
            {
                log.LogError(ex, "The chat could not answer question {UpdateId}", request.UpdateId);
                await delivery.TrySendAsync(request.ChatId, ChatTexts.Failed, ct);
                return;
            }

            if (await store.TrySaveAsync(request.ChatId, current, current.State.WithProcessed(request.UpdateId, reply.Session), ct))
            {
                await delivery.SendAnswerAsync(request.ChatId, reply.Text, ct);
                if (reply.Proposed is { Count: > 0 } changes)
                {
                    await proposals.SendAsync(request.ChatId, request.UpdateId, changes, ct);
                }

                return;
            }

            log.LogInformation("The conversation changed while answering {UpdateId}, attempt {Attempt}", request.UpdateId, attempt);
        }

        log.LogWarning("Chat question {UpdateId} dropped: the conversation kept changing", request.UpdateId);
    }
}
