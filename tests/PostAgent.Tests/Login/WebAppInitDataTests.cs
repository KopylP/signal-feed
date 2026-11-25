using PostAgent.Login;
using static PostAgent.Tests.Login.InitDataSigner;

namespace PostAgent.Tests.Login;

public class WebAppInitDataTests
{
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    [Fact]
    public void Returns_the_user_of_data_signed_with_the_bot_token()
    {
        Assert.Equal(42L, WebAppInitData.TryGetUserId(Sign(42, Now.AddMinutes(-1)), BotToken, Now, MaxAge));
    }

    [Fact]
    public void Rejects_data_signed_with_another_bot_token()
    {
        Assert.Null(WebAppInitData.TryGetUserId(Sign(42, Now, "999:other"), BotToken, Now, MaxAge));
    }

    [Fact]
    public void Rejects_data_whose_user_was_changed_after_signing()
    {
        var tampered = Sign(42, Now).Replace("%22id%22%3A42", "%22id%22%3A43", StringComparison.Ordinal);

        Assert.Null(WebAppInitData.TryGetUserId(tampered, BotToken, Now, MaxAge));
    }

    [Fact]
    public void Rejects_data_older_than_the_maximum_age()
    {
        Assert.Null(WebAppInitData.TryGetUserId(Sign(42, Now.AddMinutes(-11)), BotToken, Now, MaxAge));
    }

    [Fact]
    public void Rejects_data_from_the_future()
    {
        Assert.Null(WebAppInitData.TryGetUserId(Sign(42, Now.AddMinutes(5)), BotToken, Now, MaxAge));
    }

    [Fact]
    public void Rejects_a_repeated_field()
    {
        Assert.Null(WebAppInitData.TryGetUserId(Sign(42, Now) + "&auth_date=1", BotToken, Now, MaxAge));
    }

    [Fact]
    public void Includes_unknown_fields_in_the_check()
    {
        var data = Sign(42, Now, extra: new() { ["signature"] = "abc" });

        Assert.Equal(42L, WebAppInitData.TryGetUserId(data, BotToken, Now, MaxAge));
        Assert.Null(WebAppInitData.TryGetUserId(data.Replace("signature=abc", "signature=abd", StringComparison.Ordinal), BotToken, Now, MaxAge));
    }

    [Theory]
    [InlineData("")]
    [InlineData("auth_date=1")]
    [InlineData("hash=zz")]
    [InlineData("user=%7B%7D&auth_date=1&hash=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("garbage")]
    public void Rejects_malformed_data(string data)
    {
        Assert.Null(WebAppInitData.TryGetUserId(data, BotToken, Now, MaxAge));
    }

    [Fact]
    public void Rejects_data_without_a_bot_token()
    {
        Assert.Null(WebAppInitData.TryGetUserId(Sign(42, Now, ""), "", Now, MaxAge));
    }
}
