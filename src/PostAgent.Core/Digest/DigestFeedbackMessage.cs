using System.Globalization;
using System.Text;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Notifications;

namespace PostAgent.Digest;

// The message after the digest: the entries as a numbered list, and a row of two short buttons per entry ("👍 1", "👎 1").
// The titles live in the text, not on the buttons: half a row cuts a title to a few words. An entry without a title (Recommended)
// is named by the start of its text. The buttons carry the entry's place in the snapshot, so the vote is resolved against the digest
// the reader actually saw (FeedbackInbox): the conversation for the daily digest, IRatedDigestStore for a /digest.
public static class DigestFeedbackMessage
{
    private const int MaxTitleChars = 120;

    // Null when the digest has no entries. A post shown twice (Recommended repeats a main entry) gets one line and one row.
    public static (string Html, InlineKeyboard Keyboard)? Build(ReportSnapshot report)
    {
        var rows = new List<IReadOnlyList<KeyboardButton>>();
        var list = new StringBuilder();
        var seen = new HashSet<string>();
        for (var index = 0; index < report.Entries.Count; index++)
        {
            var entry = report.Entries[index];
            if (!seen.Add(entry.PostId))
            {
                continue;
            }

            var number = (rows.Count + 1).ToString(CultureInfo.InvariantCulture);
            var title = (string.IsNullOrWhiteSpace(entry.Title) ? entry.Text : entry.Title).Truncate(MaxTitleChars, "…");
            list.Append('\n').Append(number).Append(". ").Append(TelegramHtml.Escape(title));
            rows.Add(
            [
                new KeyboardButton($"👍 {number}", FeedbackCallback.Encode(report.ReportId, index, FeedbackVote.Up)),
                new KeyboardButton($"👎 {number}", FeedbackCallback.Encode(report.ReportId, index, FeedbackVote.Down)),
            ]);
        }

        return rows.Count == 0 ? null : (FeedbackPrompt.Message + "\n" + list, new InlineKeyboard(rows));
    }
}
