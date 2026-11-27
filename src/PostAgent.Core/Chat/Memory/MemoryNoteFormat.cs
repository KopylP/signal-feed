using System.Globalization;
using System.Text;

namespace PostAgent.Chat.Memory;

// The shape of a memory note (ADR-0022). The model gives a title, keywords and facts; the file name and the layout are made here,
// so every note reads the same and stays short:
//   # Title
//   Keywords: work, seo
//   Updated: 2026-11-27
//
//   - a fact
//   ## an optional sub-heading
public static class MemoryNoteFormat
{
    public const int MaxNotes = 10;
    public const int MaxLines = 50;
    public const int HeaderLines = 4;
    public const int MaxFactLines = MaxLines - HeaderLines;
    public const int MaxLineChars = 200;
    public const int MaxTitleChars = 80;
    public const int MaxKeywords = 5;
    public const int MaxKeywordChars = 30;
    public const int MaxNoteChars = 3000;
    private const int MaxSlugChars = 60;

    public static bool TryBuild(string? title, IReadOnlyList<string>? keywords, IReadOnlyList<string>? facts, DateOnly day, out MemoryNote? note, out string? error)
    {
        note = null;
        title = OneLine(title);
        var words = (keywords ?? []).Select(OneLine).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lines = (facts ?? []).SelectMany(f => (f ?? string.Empty).Split('\n')).Select(FactLine).Where(l => l.Length > 0).ToList();

        error = title.Length == 0 || title.Length > MaxTitleChars ? $"The title must be one line of 1 to {MaxTitleChars} characters."
            : words.Count == 0 || words.Count > MaxKeywords ? $"Give 1 to {MaxKeywords} keywords."
            : words.Any(k => k.Length > MaxKeywordChars) ? $"A keyword is at most {MaxKeywordChars} characters."
            : lines.Count == 0 ? "Give at least one fact."
            : lines.Count > MaxFactLines ? $"Too many lines: a note holds at most {MaxFactLines} lines of facts. Keep only what matters, or split the topic."
            : lines.Any(l => l.Length > MaxLineChars) ? $"A line is at most {MaxLineChars} characters. Shorten it or split it."
            : null;
        if (error is not null)
        {
            return false;
        }

        var slug = Slug(words);
        if (slug.Length == 0)
        {
            error = "The keywords must contain letters or digits.";
            return false;
        }

        var text = new StringBuilder()
            .Append("# ").Append(title).Append('\n')
            .Append("Keywords: ").Append(string.Join(", ", words)).Append('\n')
            .Append("Updated: ").Append(DayText(day)).Append("\n\n")
            .Append(string.Join('\n', lines)).Append('\n')
            .ToString();
        if (text.Length > MaxNoteChars)
        {
            error = $"The note is too long (at most {MaxNoteChars} characters). Keep only what matters.";
            return false;
        }

        note = new MemoryNote($"{DayText(day)}-{slug}.md", text);
        return true;
    }

    // The keywords as a file name part: lower case letters and digits joined by dashes, in any script.
    public static string Slug(IEnumerable<string> keywords)
    {
        var slug = new StringBuilder();
        foreach (var c in string.Join('-', keywords).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var text = slug.ToString().Trim('-');
        return text.Length <= MaxSlugChars ? text : text[..MaxSlugChars].TrimEnd('-');
    }

    // A note name as the model may write it: the stored names are matched without case and with or without ".md".
    public static bool SameName(string fileName, string? asked)
    {
        var name = (asked ?? string.Empty).Trim();
        return fileName.Equals(name, StringComparison.OrdinalIgnoreCase) || fileName.Equals(name + ".md", StringComparison.OrdinalIgnoreCase);
    }

    private static string DayText(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string OneLine(string? text) => string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // A fact becomes a bullet; a line the model marked as a sub-heading stays one. Its own bullet marks are dropped, so there is never "- - fact".
    private static string FactLine(string line)
    {
        var text = OneLine(line);
        if (text.StartsWith("## ", StringComparison.Ordinal))
        {
            var heading = text.TrimStart('#').Trim();
            return heading.Length == 0 ? string.Empty : "## " + heading;
        }

        text = text.TrimStart('-', '*', '•', '#', ' ');
        return text.Length == 0 ? string.Empty : "- " + text;
    }
}
