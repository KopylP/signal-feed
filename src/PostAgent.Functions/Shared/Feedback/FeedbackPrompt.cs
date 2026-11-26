using System.Text;

namespace PostAgent.Shared.Feedback;

public static class FeedbackPrompt
{
    // The message under the digest, sent by Digest and kept when a button is marked by Feedback. Ukrainian: it goes to the end user.
    public const string Message = "👍 👎 <b>Оцініть записи</b>\nТак наступні дайджести стануть точнішими.";

    // The block for the scoring request: topics the reader liked (+) and disliked (-), newest last. Every "<" is escaped so that
    // a topic cannot close the tag. Empty when there is no feedback yet.
    public static string Block(IReadOnlyList<FeedbackItem> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var body = new StringBuilder();
        foreach (var item in items)
        {
            body.Append(item.Vote == FeedbackVote.Up ? "+ " : "- ")
                .Append(item.Topic.Replace('\n', ' ').Replace("<", "&lt;", StringComparison.Ordinal))
                .Append(" (@").Append(item.Source.Replace("<", "&lt;", StringComparison.Ordinal)).AppendLine(")");
        }

        return $"<reader_feedback>\n{body.ToString().TrimEnd()}\n</reader_feedback>";
    }
}
