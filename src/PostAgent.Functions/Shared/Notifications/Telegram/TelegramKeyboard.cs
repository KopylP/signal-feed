using Telegram.Bot.Types.ReplyMarkups;

namespace PostAgent.Shared.Notifications.Telegram;

public static class TelegramKeyboard
{
    // Null for an empty keyboard: Telegram would keep the old buttons on an edit with an empty markup object.
    public static InlineKeyboardMarkup? ToMarkup(InlineKeyboard keyboard) =>
        keyboard.Rows.Count == 0
            ? null
            : new InlineKeyboardMarkup(keyboard.Rows.Select(row => row.Select(ToButton)));

    private static InlineKeyboardButton ToButton(KeyboardButton button)
    {
        var result = InlineKeyboardButton.WithCallbackData(button.Text, button.Data);
        result.Style = button.Style switch
        {
            ButtonStyle.Primary => KeyboardButtonStyle.Primary,
            ButtonStyle.Success => KeyboardButtonStyle.Success,
            ButtonStyle.Danger => KeyboardButtonStyle.Danger,
            _ => null,
        };
        return result;
    }
}
