using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginPageUrlTests
{
    [Fact]
    public void Points_at_the_page_of_the_function_app_over_https()
    {
        var url = LoginPageUrl.Build("func-signal.azurewebsites.net", "a-b_c", LoginPageUrl.CodeStage);

        Assert.Equal("https://func-signal.azurewebsites.net/api/telegram/login?a=a-b_c&s=code", url?.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("evil.example/path")]
    public void Has_no_url_without_a_plain_host(string? host)
    {
        Assert.Null(LoginPageUrl.Build(host, "id", LoginPageUrl.PhoneStage));
    }
}
