using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using KeyboardButton = PostAgent.Shared.Notifications.KeyboardButton;

namespace PostAgent.Feedback;

// A press of a 👍 or 👎 under the digest. Registered before the settings menu, which swallows every button it does not know.
// The daily digest is found in the conversation, a /digest in IRatedDigestStore.
public sealed class FeedbackInbox(
    ChatResolver resolver,
    IChatStateStore state,
    IRatedDigestStore rated,
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

        ReportSnapshot? report;
        try
        {
            report = await FindReportAsync(message.Chat.Id, reportId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The digest of a vote could not be read");
            await panel.AnswerCallbackAsync(query.Id, FeedbackTexts.Failed, ct);
            return true;
        }

        if (report is null || index >= report.Entries.Count)
        {
            await panel.AnswerCallbackAsync(query.Id, FeedbackTexts.Outdated, ct);
            return true;
        }

        var entry = report.Entries[index];
        try
        {
            await store.RecordAsync(new FeedbackItem(entry.PostId, entry.Source, Topic(entry), vote, time.GetUtcNow()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "A vote could not be saved");
            await panel.AnswerCallbackAsync(query.Id, FeedbackTexts.Failed, ct);
            return true;
        }

        await MarkAsync(message, data, ct);
        await AnswerAsync(query.Id, vote == FeedbackVote.Up ? FeedbackTexts.ThanksUp : FeedbackTexts.ThanksDown, ct);
        return true;
    }

    private async Task<ReportSnapshot?> FindReportAsync(long chatId, string reportId, CancellationToken ct) =>
        (await state.LoadAsync(chatId, ct))?.State.Report is { } current && current.ReportId == reportId
            ? current
            : await rated.FindAsync(chatId, reportId, ct);

    // What the scorer reads as the topic: the digest title, or the text of an entry that has none (Recommended), cut so a vote stays small.
    private static string Topic(SnapshotEntry entry) =>
        (string.IsNullOrWhiteSpace(entry.Title) ? entry.Text : entry.Title).Truncate(FeedbackLog.MaxTopicChars, "…") is { Length: > 0 } topic ? topic : entry.Source;

    // A spinner that cannot be stopped ("query is too old") must not undo the vote or the ✅.
    private async Task AnswerAsync(string queryId, string? text, CancellationToken ct)
    {
        try
        {
            await panel.AnswerCallbackAsync(queryId, text, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation(ex, "A rating button could not be answered");
        }
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
    // The other rows keep theirs, so every rated entry stays marked.
    private static KeyboardButton Mark(InlineKeyboardButton button, string pressed, IEnumerable<InlineKeyboardButton> row)
    {
        if (!row.Any(b => b.CallbackData == pressed))
        {
            return new KeyboardButton(button.Text, button.CallbackData ?? string.Empty);
        }

        var text = button.Text.StartsWith("✅ ", StringComparison.Ordinal) ? button.Text[2..] : button.Text;
        return new KeyboardButton(button.CallbackData == pressed ? "✅ " + text : text, button.CallbackData ?? string.Empty);
    }
}
