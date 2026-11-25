using PostAgent.Shared.Notifications;
using PostAgent.Shared.Notifications.Telegram;
using Telegram.Bot.Types.ReplyMarkups;
using KeyboardButton = PostAgent.Shared.Notifications.KeyboardButton;

namespace PostAgent.Tests.Shared.Notifications;

public class TelegramKeyboardTests
{
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

    [Fact]
    public void An_empty_keyboard_is_no_markup()
    {
        Assert.Null(TelegramKeyboard.ToMarkup(InlineKeyboard.Empty));
    }
}
