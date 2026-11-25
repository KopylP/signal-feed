using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginCommandTests
{
    [Theory]
    [InlineData("/login", true)]
    [InlineData(" /LOGIN ", true)]
    [InlineData("/login@signal_bot", true)]
    [InlineData("/login now", false)]
    [InlineData("/loginx", false)]
    [InlineData("login", false)]
    [InlineData(null, false)]
    public void Recognizes_the_bare_command(string? text, bool expected)
    {
        Assert.Equal(expected, LoginCommand.Is(text));
    }
}
