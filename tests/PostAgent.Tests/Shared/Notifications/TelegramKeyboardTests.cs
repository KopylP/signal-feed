using PostAgent.Shared.Notifications;
using PostAgent.Shared.Notifications.Telegram;

namespace PostAgent.Tests.Shared.Notifications;

public class TelegramKeyboardTests
{
    [Fact]
    public void Rejects_callback_data_longer_than_64_bytes()
    {
        Assert.Throws<ArgumentException>(() => new KeyboardButton("x", new string('a', 65)));
    }

    [Fact]
    public void Counts_bytes_not_characters()
    {
        // 33 Cyrillic letters are 66 bytes in UTF-8.
        Assert.Throws<ArgumentException>(() => new KeyboardButton("x", new string('я', 33)));
        Assert.Equal(new string('a', 64), new KeyboardButton("x", new string('a', 64)).Data);
    }

    [Fact]
    public void Maps_rows_and_buttons_to_telegram_markup()
    {
        var keyboard = new InlineKeyboard([
            [new KeyboardButton("−", "n:DigestSize:6"), new KeyboardButton("+", "n:DigestSize:8")],
            [new KeyboardButton("Назад", "m")],
        ]);

        var markup = TelegramKeyboard.ToMarkup(keyboard);

        Assert.NotNull(markup);
        var rows = markup.InlineKeyboard.Select(r => r.ToList()).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(["−", "+"], rows[0].Select(b => b.Text));
        Assert.Equal(["n:DigestSize:6", "n:DigestSize:8"], rows[0].Select(b => b.CallbackData));
        Assert.Equal("m", rows[1][0].CallbackData);
    }

    [Fact]
    public void An_empty_keyboard_has_no_markup()
    {
        Assert.Null(TelegramKeyboard.ToMarkup(InlineKeyboard.Empty));
    }
}
