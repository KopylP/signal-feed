using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsOverridesJsonTests
{
    [Fact]
    public void Round_trips_overrides_through_json()
    {
        var expires = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        var original = new SettingsOverrides(
            new Dictionary<string, string> { ["DigestSize"] = "9" },
            [new SourceEntry("news", true), new SourceEntry("fresh", false)],
            ["news"],
            new PendingInput("Sources", "add", 42, expires),
            1234);

        Assert.True(SettingsOverridesJson.TryRead(SettingsOverridesJson.Write(original), out var read));

        Assert.Equal("9", read.Values["DigestSize"]);
        Assert.Equal([new SourceEntry("news", true), new SourceEntry("fresh", false)], read.Sources);
        Assert.Equal(["news"], read.AlertSources);
        Assert.Equal(new PendingInput("Sources", "add", 42, expires), read.Pending);
        Assert.Equal(1234, read.LastUpdateId);
    }

    [Fact]
    public void Empty_overrides_keep_the_lists_unset()
    {
        Assert.True(SettingsOverridesJson.TryRead(SettingsOverridesJson.Write(SettingsOverrides.Empty), out var read));

        Assert.Empty(read.Values);
        Assert.Null(read.Sources);
        Assert.Null(read.AlertSources);
        Assert.Null(read.Pending);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    public void Rejects_broken_json_without_throwing(string json)
    {
        Assert.False(SettingsOverridesJson.TryRead(json, out _));
    }
}
