using PostAgent.Shared.Notifications;
using PostAgent.Shared.Notifications.Telegram;
using Telegram.Bot.Types.ReplyMarkups;
using KeyboardButton = PostAgent.Shared.Notifications.KeyboardButton;

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
    public void Maps_a_mini_app_button_to_a_web_app_button_without_callback_data()
    {
        var url = new Uri("https://func-signal.azurewebsites.net/api/telegram/login?a=x&s=code");

        var markup = TelegramKeyboard.ToMarkup(new InlineKeyboard([[KeyboardButton.OpenWebApp("Ввести код", url)]]));

        Assert.NotNull(markup);
        var button = Assert.Single(Assert.Single(markup.InlineKeyboard));
        Assert.Equal(url.AbsoluteUri, button.WebApp?.Url);
        Assert.Null(button.CallbackData);
    }

    [Fact]
    public void Refuses_a_mini_app_that_is_not_served_over_https()
    {
        Assert.Throws<ArgumentException>(() => KeyboardButton.OpenWebApp("x", new Uri("http://example.com/")));
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

    private static InlineKeyboardButton Single(KeyboardButton button) =>
        TelegramKeyboard.ToMarkup(new InlineKeyboard([[button]]))!.InlineKeyboard.Single().Single();

    [Theory]
    [InlineData(ButtonStyle.Success, KeyboardButtonStyle.Success)]
    [InlineData(ButtonStyle.Danger, KeyboardButtonStyle.Danger)]
    [InlineData(ButtonStyle.Primary, KeyboardButtonStyle.Primary)]
    public void A_style_becomes_the_color_of_the_button(ButtonStyle style, KeyboardButtonStyle expected)
    {
        var button = Single(new KeyboardButton("Так", "yes", style));

        Assert.Equal(expected, button.Style);
        Assert.Equal("yes", button.CallbackData);
        Assert.Equal("Так", button.Text);
    }

    [Fact]
    public void A_button_without_a_style_leaves_the_color_to_the_app()
    {
        Assert.Null(Single(new KeyboardButton("Так", "yes")).Style);
    }
}
