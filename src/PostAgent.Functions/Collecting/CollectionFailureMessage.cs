using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Collecting;

public static class CollectionFailureMessage
{
    private const int MaxLines = 10;

    // A button per failed source that was only just added from the bot: most likely a mistyped name, and the owner removes it in one tap.
    public static InlineKeyboard RemovalKeyboard(IEnumerable<string> usernames) =>
        new(usernames
            .Take(MaxLines)
            .Select(name => (IReadOnlyList<KeyboardButton>)[new KeyboardButton($"🗑 Видалити @{name}", MenuCallbacks.RemoveSource(name), ButtonStyle.Danger)])
            .ToList());

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
