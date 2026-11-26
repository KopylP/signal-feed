using PostAgent.Shared.Notifications;

namespace PostAgent.Chat.Answering;

public static class ChatReplyFormatter
{
    // Telegram allows 4096 characters; the limit counts the escaped text, which can be several times longer than the raw one.
    private const int MaxEscapedChars = 3800;

    private static readonly string[] Separators = ["\n\n", "\n", " "];

    // The raw text is cut first and each part is escaped afterwards, so an entity such as &amp; is never cut in two.
    public static IReadOnlyList<string> Split(string text)
    {
        var parts = new List<string>();
        var rest = text.Trim();
        while (rest.Length > 0)
        {
            var cut = CutPoint(rest);
            var part = rest[..cut].Trim();
            if (part.Length > 0)
            {
                parts.Add(TelegramHtml.Escape(part));
            }

            rest = rest[cut..].TrimStart();
        }

        return parts;
    }

    private static int CutPoint(string text)
    {
        var escapedLength = 0;
        var end = 0;
        while (end < text.Length)
        {
            var unit = char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1;
            var grown = TelegramHtml.Escape(text.Substring(end, unit)).Length;
            if (escapedLength + grown > MaxEscapedChars)
            {
                break;
            }

            escapedLength += grown;
            end += unit;
        }

        if (end >= text.Length)
        {
            return text.Length;
        }

        // Prefer a paragraph, then a line, then a space, as long as the part does not become tiny.
        var window = text[..end];
        foreach (var separator in Separators)
        {
            var at = window.LastIndexOf(separator, StringComparison.Ordinal);
            if (at > end / 2)
            {
                return at;
            }
        }

        return end;
    }
}
