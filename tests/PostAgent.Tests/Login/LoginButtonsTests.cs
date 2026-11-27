using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginButtonsTests
{
    [Fact]
    public void Each_stage_opens_the_form_for_its_own_field()
    {
        var buttons = LoginButtons.Build("agent.example.net", "attempt-1");

        Assert.NotNull(buttons);
        Assert.EndsWith("s=phone", WebApp(buttons.Phone), StringComparison.Ordinal);
        Assert.EndsWith("s=code", WebApp(buttons.Code), StringComparison.Ordinal);
        Assert.EndsWith("s=password", WebApp(buttons.Password), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("host/path")]
    public void There_are_no_buttons_without_a_valid_host(string? host)
    {
        Assert.Null(LoginButtons.Build(host, "attempt-1"));
    }

    private static string? WebApp(PostAgent.Shared.Notifications.InlineKeyboard keyboard) =>
        Assert.Single(Assert.Single(keyboard.Rows)).WebApp?.AbsoluteUri;
}
