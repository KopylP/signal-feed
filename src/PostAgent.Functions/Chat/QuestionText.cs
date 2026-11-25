using PostAgent.Shared.Common;
using Telegram.Bot.Types;

namespace PostAgent.Chat;

// The text of a question, with what the reader pointed at: a quoted part of a message, or the message they replied to.
// Without it "expand on this" reaches the model with no "this". The quoted text may come from a channel (a forwarded post),
// so it goes inside a <quote> block, as data.
public static class QuestionText
{
    public const int MaxQuoteChars = 800;

    public static string From(Message message, string text)
    {
        var quoted = message.Quote?.Text;
        if (string.IsNullOrWhiteSpace(quoted))
        {
            quoted = message.ReplyToMessage is { } reply ? reply.Text ?? reply.Caption : null;
        }

        return string.IsNullOrWhiteSpace(quoted)
            ? text.Trim()
            : $"{DataBlock.Wrap("quote", quoted.Trim().Truncate(MaxQuoteChars))}\n{text.Trim()}";
    }
}
