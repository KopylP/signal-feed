using System.Text;

namespace PostAgent.Shared.Feedback;

public static class FeedbackPrompt
{
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
                .Append(Clean(item.Topic))
                .Append(" (@").Append(Clean(item.Source)).AppendLine(")");
        }

        return $"<reader_feedback>\n{body.ToString().TrimEnd()}\n</reader_feedback>";
    }

    // One line, no way to close the tag: every line break a model may read (\r, U+0085, U+2028, U+2029) and every control character becomes a space.
    private static string Clean(string text) =>
        string.Concat(text.Select(c => char.IsControl(c) || c is '\u2028' or '\u2029' ? ' ' : c))
            .Replace("<", "&lt;", StringComparison.Ordinal);
}
