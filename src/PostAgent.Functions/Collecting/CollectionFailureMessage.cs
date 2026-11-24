using PostAgent.Notifications;

namespace PostAgent.Collecting;

public static class CollectionFailureMessage
{
    private const int MaxLines = 10;

    public static string Format(IReadOnlyList<string> failures)
    {
        var lines = failures.Take(MaxLines).Select(TelegramHtml.Escape).ToList();
        if (failures.Count > MaxLines)
        {
            lines.Add($"…і ще {failures.Count - MaxLines}");
        }
        return "⚠️ <b>Збір не вдався</b>\n" + string.Join('\n', lines);
    }
}
