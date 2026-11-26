using System.Globalization;
using System.Text;

namespace PostAgent.Shared.Settings;

// The topics the owner asked to follow ("google core update"): short phrases, every word of one must be in a post for it to match.
// Stored as one App Setting string, so a phrase never holds the separator.
public static class WatchPhrases
{
    public const char Separator = ';';
    public const int MaxCount = 20;
    public const int MaxChars = 100;

    // The punctuation a word may end with and still mean the word itself: "c#", "c++".
    private const string KeptAtEnd = "#+";

    // One line, single spaces, no separator or angle brackets; quotes, commas and the like around a word are dropped, so «"google core update"» follows
    // the words and not the quote marks. Null when nothing is left or it is too long.
    public static string? Normalize(string? text)
    {
        var cleaned = new StringBuilder();
        foreach (var c in text ?? string.Empty)
        {
            // Invisible format characters (zero-width space, soft hyphen, bidi marks) are dropped, so a word stays one word and a line cannot be reordered.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                continue;
            }

            cleaned.Append(char.IsControl(c) || c is Separator or '<' or '>' ? ' ' : c);
        }

        var words = cleaned.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(TrimEdges)
            .Where(w => w.Length > 0);
        var phrase = string.Join(' ', words);
        return phrase.Length is > 0 and <= MaxChars ? phrase : null;
    }

    public static IReadOnlyList<string> Parse(string? setting) =>
        (setting ?? string.Empty).Split(Separator)
            .Select(Normalize).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCount)
            .ToList();

    public static string Join(IEnumerable<string> phrases) => string.Join(Separator, phrases);

    private static string TrimEdges(string word)
    {
        var start = 0;
        while (start < word.Length && IsMark(word[start]))
        {
            start++;
        }

        var end = word.Length;
        while (end > start && IsMark(word[end - 1]) && !KeptAtEnd.Contains(word[end - 1]))
        {
            end--;
        }

        return word[start..end];
    }

    private static bool IsMark(char c) => char.IsPunctuation(c) || char.IsSymbol(c);
}
