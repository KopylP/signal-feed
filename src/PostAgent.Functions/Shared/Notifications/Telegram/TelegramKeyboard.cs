using Telegram.Bot.Types.ReplyMarkups;

namespace PostAgent.Shared.Notifications.Telegram;

public static class TelegramKeyboard
{
    // Null for an empty keyboard: Telegram would keep the old buttons on an edit with an empty markup object.
    public static InlineKeyboardMarkup? ToMarkup(InlineKeyboard keyboard) =>
        keyboard.Rows.Count == 0
            ? null
            : new InlineKeyboardMarkup(keyboard.Rows.Select(row => row.Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.Data))));
}
