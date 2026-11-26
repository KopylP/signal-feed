using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Notifications;

namespace PostAgent.Digest;

// An entry without a title (Recommended) is named by the start of its text, so two of one channel are not the same button.
// The message after the digest with a row of two buttons per entry. The buttons name the entry by its title and carry its place in the
// snapshot, so the vote is resolved against the digest the reader actually saw (FeedbackInbox): the conversation for the daily digest,
// IRatedDigestStore for a /digest.
public static class DigestFeedbackMessage
{
    private const int MaxLabelChars = 28;

    // Null when the digest has no entries. A post shown twice (Recommended repeats a main entry) gets one row.
    public static (string Html, InlineKeyboard Keyboard)? Build(ReportSnapshot report)
    {
        var rows = new List<IReadOnlyList<KeyboardButton>>();
        var seen = new HashSet<string>();
        for (var index = 0; index < report.Entries.Count; index++)
        {
            var entry = report.Entries[index];
            if (!seen.Add(entry.PostId))
            {
                continue;
            }

            var title = (string.IsNullOrWhiteSpace(entry.Title) ? entry.Text : entry.Title).Truncate(MaxLabelChars, "…");
            rows.Add(
            [
                new KeyboardButton($"👍 {title}", FeedbackCallback.Encode(report.ReportId, index, FeedbackVote.Up)),
                new KeyboardButton("👎", FeedbackCallback.Encode(report.ReportId, index, FeedbackVote.Down)),
            ]);
        }

        return rows.Count == 0 ? null : (FeedbackPrompt.Message, new InlineKeyboard(rows));
    }
}
