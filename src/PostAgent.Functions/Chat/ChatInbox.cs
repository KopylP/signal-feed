using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Chat;

// The webhook must answer Telegram fast, so a question is only checked and queued here; the answer is made by ChatProcessor.
public sealed class ChatInbox(
    ChatResolver resolver,
    IChatStateStore state,
    IChatQueue queue,
    ITelegramNotifier notifier,
    ILogger<ChatInbox> log) : IIncomingMessageHandler
{
    private const int MaxQuestionChars = 2000;

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        // A message that starts with "/" is a bot command (pairing's /start, for one), not a question.
        if (update.Message is not { Text: { } text } message || string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith('/'))
        {
            return;
        }

        // Only the private chat with the bot is served: in a group every member's message would cost a model call.
        if (message.Chat.Type != ChatType.Private || await resolver.TryGetChatIdAsync(ct) != message.Chat.Id)
        {
            return;
        }

        var current = await state.LoadAsync(ct);
        if (current is null)
        {
            await notifier.SendAsync(ChatTexts.NoReport, ct);
            return;
        }

        try
        {
            await queue.EnqueueAsync(new ChatRequest(update.Id, text.Trim().Truncate(MaxQuestionChars), current.State.Report.ReportId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Telegram would resend the update on a 5xx, but only for a while; the reader can just ask again.
            log.LogError(ex, "A chat question could not be queued");
        }
    }
}
