namespace PostAgent.Shared.Settings;

// One reviewed edit of a text: a fragment replaced by another. The owner sees exactly this pair before it is applied,
// so the fragment must be found as it is written, once; the model never rewrites a whole text.
public static class TextEdit
{
    public const int MaxSnippet = 500;
    public const int MaxText = 2000;
    public const int MaxEditsPerPlan = 3;

    // before empty: after is added at the end. after empty: the fragment is removed.
    public static bool TryApply(string current, string? before, string? after, out string result, out string? error)
    {
        result = current;
        error = null;
        before = before?.Trim() ?? string.Empty;
        after = after?.Trim() ?? string.Empty;
        if (before.Length > MaxSnippet || after.Length > MaxSnippet)
        {
            error = $"Фрагмент задовгий (до {MaxSnippet} символів). Розбий правку на менші.";
            return false;
        }

        if (before.Length == 0)
        {
            if (after.Length == 0)
            {
                error = "Порожня правка.";
                return false;
            }

            result = current.Trim().Length == 0 ? after : current.TrimEnd() + "\n" + after;
        }
        else
        {
            var first = current.IndexOf(before, StringComparison.Ordinal);
            if (first < 0)
            {
                error = "Фрагмент не знайдено в поточному тексті. Візьми його дослівно з get_prompt або get_profile.";
                return false;
            }

            if (current.IndexOf(before, first + 1, StringComparison.Ordinal) >= 0)
            {
                error = "Фрагмент зустрічається кілька разів. Візьми довший, щоб він був унікальним.";
                return false;
            }

            result = (current[..first] + after + current[(first + before.Length)..]).Trim();
        }

        if (result.Length > MaxText)
        {
            error = $"Текст після правки задовгий (до {MaxText} символів).";
            result = current;
            return false;
        }

        return true;
    }

    // A section keeps its place in the prompt: no new headings, no markers, never empty (reset it instead).
    public static string? CheckSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Секція не може бути порожньою. Скинь її до стандартної.";
        }

        if (text.Contains("<!--", StringComparison.Ordinal) || text.Split('\n').Any(l => l.TrimStart().StartsWith('#')))
        {
            return "У секції не можна додавати заголовки (#) та службові маркери.";
        }

        return null;
    }
}
