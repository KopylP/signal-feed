using PostAgent.Shared.Chat;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using KeyboardButton = PostAgent.Shared.Notifications.KeyboardButton;

namespace PostAgent.Feedback;

// A press of a 👍 or 👎 under the digest. Registered before the settings menu, which swallows every button it does not know.
public sealed class FeedbackInbox(
    ChatResolver resolver,
    IChatStateStore state,
    IFeedbackStore store,
    ITelegramPanel panel,
    TimeProvider time,
    ILogger<FeedbackInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.CallbackQuery is not { Data: { } data } query || !data.StartsWith(FeedbackCallback.Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Message is not { } message
            || message.Chat.Type != ChatType.Private
            || !await resolver.IsBoundAsync(message.Chat.Id, ct))
        {
            await panel.AnswerCallbackAsync(query.Id, null, ct);
            return true;
        }

        if (!FeedbackCallback.TryDecode(data, out var reportId, out var index, out var vote))
        {
            await panel.AnswerCallbackAsync(query.Id, null, ct);
            return true;
        }

        var report = (await state.LoadAsync(message.Chat.Id, ct))?.State.Report;
        if (report is null || report.ReportId != reportId || index >= report.Entries.Count)
        {
            await panel.AnswerCallbackAsync(query.Id, FeedbackTexts.Outdated, ct);
            return true;
        }

        var entry = report.Entries[index];
        try
        {
            await store.RecordAsync(new FeedbackItem(entry.PostId, entry.Source, entry.Title ?? entry.Source, vote, time.GetUtcNow()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "A vote could not be saved");
            await panel.AnswerCallbackAsync(query.Id, FeedbackTexts.Failed, ct);
            return true;
        }

        await panel.AnswerCallbackAsync(query.Id, vote == FeedbackVote.Up ? FeedbackTexts.ThanksUp : FeedbackTexts.ThanksDown, ct);
        await MarkAsync(message, data, ct);
        return true;
    }

    // The pressed button gets a ✅, so the reader sees what was recorded. A failure here changes nothing about the vote.
    private async Task MarkAsync(Message message, string pressed, CancellationToken ct)
    {
        try
        {
            var rows = message.ReplyMarkup?.InlineKeyboard
                .Select(row => (IReadOnlyList<KeyboardButton>)row
                    .Select(b => Mark(b, pressed, row))
                    .ToList())
                .ToList();
            if (rows is { Count: > 0 })
            {
                await panel.EditAsync(message.Chat.Id, message.Id, FeedbackPrompt.Message, new InlineKeyboard(rows), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation(ex, "The rating buttons could not be updated");
        }
    }

    // Only the row that holds the pressed button changes: the pressed one gets ✅ and the other loses its own, if it had one.
    private static KeyboardButton Mark(InlineKeyboardButton button, string pressed, IEnumerable<InlineKeyboardButton> row)
    {
        var text = button.Text.StartsWith("✅ ", StringComparison.Ordinal) ? button.Text[2..] : button.Text;
        var inRow = row.Any(b => b.CallbackData == pressed);
        return new KeyboardButton(inRow && button.CallbackData == pressed ? "✅ " + text : text, button.CallbackData ?? string.Empty);
    }
}
