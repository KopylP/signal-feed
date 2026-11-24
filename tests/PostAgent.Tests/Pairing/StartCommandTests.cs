using PostAgent.Pairing;

namespace PostAgent.Tests.Pairing;

public class StartCommandTests
{
    [Theory]
    [InlineData("/start abc123", "abc123")]
    [InlineData("  /start   abc123  ", "abc123")]
    [InlineData("/start@signal_feed_bot abc123", "abc123")]
    public void Key_is_taken_from_the_start_command(string text, string expected)
    {
        Assert.Equal(expected, StartCommand.ParseKey(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/start")]
    [InlineData("hello abc123")]
    [InlineData("/starting abc123")]
    [InlineData("abc123")]
    public void Anything_else_has_no_key(string? text)
    {
        Assert.Null(StartCommand.ParseKey(text));
    }
}
