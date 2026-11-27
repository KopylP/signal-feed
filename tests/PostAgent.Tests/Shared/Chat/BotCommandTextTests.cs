using PostAgent.Shared.Chat;

namespace PostAgent.Tests.Shared.Chat;

public class BotCommandTextTests
{
    [Theory]
    [InlineData("/stats", "/stats", "")]
    [InlineData("  /stats   30  ", "/stats", "30")]
    [InlineData("/STATS 30", "/stats", "30")]
    [InlineData("/digest@signal_feed_bot today", "/digest", "today")]
    [InlineData("/digest 2026-11-25T18:00 2026-11-25T23:00", "/digest", "2026-11-25T18:00 2026-11-25T23:00")]
    [InlineData("/start\tKey", "/start", "Key")]
    public void Splits_a_command_into_its_lower_case_name_and_the_arguments(string text, string name, string arguments)
    {
        Assert.Equal(new BotCommandText(name, arguments), BotCommandText.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello /stats")]
    [InlineData("stats")]
    public void Text_that_does_not_start_with_a_slash_is_not_a_command(string? text)
    {
        Assert.Null(BotCommandText.Parse(text));
    }
}
