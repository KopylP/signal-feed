using System.Text;

namespace PostAgent.Shared.Settings;

// The topics the owner asked to follow ("google core update"): short phrases, every word of one must be in a post for it to match.
// Stored as one App Setting string, so a phrase never holds the separator.
public static class WatchPhrases
{
    public const char Separator = ';';
    public const int MaxCount = 20;
    public const int MaxChars = 100;

    // One line, single spaces, no separator or angle brackets; null when nothing is left or it is too long.
    public static string? Normalize(string? text)
    {
        var cleaned = new StringBuilder();
        foreach (var c in text ?? string.Empty)
        {
            cleaned.Append(char.IsControl(c) || c is Separator or '<' or '>' ? ' ' : c);
        }

        var phrase = string.Join(' ', cleaned.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return phrase.Length is > 0 and <= MaxChars ? phrase : null;
    }

    public static IReadOnlyList<string> Parse(string? setting) =>
        (setting ?? string.Empty).Split(Separator)
            .Select(Normalize).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCount)
            .ToList();

    public static string Join(IEnumerable<string> phrases) => string.Join(Separator, phrases);
}
