using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class SourceRulesTests
{
    [Theory]
    [InlineData("@MyChannel", "mychannel")]
    [InlineData("t.me/mychannel", "mychannel")]
    [InlineData("HTTPS://T.ME/MyChannel", "mychannel")]
    [InlineData("https://t.me/my_channel/", "my_channel")]
    [InlineData("  some_group  ", "some_group")]
    public void Normalizes_the_forms_a_username_is_typed_in(string typed, string expected)
    {
        Assert.Equal(expected, SourceRules.TryParse(typed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("bad name")]
    [InlineData("імя_каналу")]
    [InlineData("a-b-c-d-e")]
    [InlineData("t.me/mychannel/123")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Rejects_what_is_not_a_telegram_username(string typed)
    {
        Assert.Null(SourceRules.TryParse(typed));
    }
}
