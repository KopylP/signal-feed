using PostAgent.Chat.Infrastructure;
using PostAgent.Shared.Settings;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat;

public class SettingsToolsTests
{
    private readonly MenuHarness _h = new();
    private readonly List<SettingChange> _draft = [];

    private SettingsTools Tools() => _h.ToolsFactory.Create(_draft);

    [Fact]
    public async Task A_valid_step_goes_into_the_plan_and_nothing_is_saved()
    {
        var answer = await Tools().ProposeSet("DigestSize", "8", CancellationToken.None);

        Assert.Equal([new SettingChange(SettingChangeKind.Set, "DigestSize", "8")], _draft);
        Assert.Contains("Nothing is changed yet", answer);
        Assert.Null(_h.Store.Current);
    }

    [Fact]
    public async Task A_value_out_of_range_is_rejected_and_stays_out_of_the_plan()
    {
        var answer = await Tools().ProposeSet("DigestSize", "9999", CancellationToken.None);

        Assert.StartsWith("Rejected", answer);
        Assert.Empty(_draft);
    }

    [Fact]
    public async Task An_unknown_key_such_as_a_secret_is_rejected()
    {
        var answer = await Tools().ProposeSet("OpenAIApiKey", "sk-evil", CancellationToken.None);

        Assert.StartsWith("Rejected", answer);
        Assert.Empty(_draft);
    }

    [Fact]
    public async Task The_same_setting_proposed_twice_keeps_the_last_value_only()
    {
        var tools = Tools();

        await tools.ProposeSet("DigestSize", "8", CancellationToken.None);
        await tools.ProposeSet("DigestSize", "6", CancellationToken.None);

        Assert.Equal([new SettingChange(SettingChangeKind.Set, "DigestSize", "6")], _draft);
    }

    [Fact]
    public async Task Quiet_hours_can_be_turned_off_with_a_dash()
    {
        await Tools().ProposeSet("AlertQuietHours", "-", CancellationToken.None);

        Assert.Equal([new SettingChange(SettingChangeKind.Set, "AlertQuietHours", string.Empty)], _draft);
    }

    [Fact]
    public async Task Removing_a_source_that_is_not_there_asks_for_the_list()
    {
        var answer = await Tools().ProposeRemoveSource("nothere", CancellationToken.None);

        Assert.Contains("get_settings", answer);
        Assert.Empty(_draft);
    }

    [Fact]
    public async Task Removing_the_last_source_is_rejected()
    {
        var tools = Tools();
        await tools.ProposeRemoveSource("news", CancellationToken.None);

        var answer = await tools.ProposeRemoveSource("chat", CancellationToken.None);

        Assert.StartsWith("Rejected", answer);
        Assert.Equal([new SettingChange(SettingChangeKind.RemoveSource, "news")], _draft);
    }

    [Fact]
    public async Task A_new_source_is_normalized_and_a_bad_name_is_refused()
    {
        var tools = Tools();

        await tools.ProposeAddSource("https://t.me/NewChannel", CancellationToken.None);
        var bad = await tools.ProposeAddSource("a b", CancellationToken.None);

        Assert.Equal([new SettingChange(SettingChangeKind.AddSource, "newchannel")], _draft);
        Assert.Contains("not a valid", bad);
    }

    [Fact]
    public async Task The_plan_is_capped()
    {
        var tools = Tools();
        foreach (var key in new[] { "DigestSize", "InterestingThreshold", "AlertThreshold", "BurstMinPosts", "AlertWindowHours", "AlertCooldownHours", "AlertMaxPerRun", "MinPostChars" })
        {
            Assert.StartsWith("Added", await tools.ProposeSet(key, "2", CancellationToken.None));
        }

        var answer = await tools.ProposeSet("MaxItemsPerGroup", "2", CancellationToken.None);

        Assert.Contains("Too many", answer);
        Assert.Equal(SettingsProposal.MaxChanges, _draft.Count);
    }

    [Fact]
    public async Task The_settings_list_shows_values_ranges_and_sources()
    {
        var text = await Tools().GetSettings(CancellationToken.None);

        Assert.Contains("DigestSize", text);
        Assert.Contains("OpenAIChatModel", text);
        Assert.Contains("@news (alerts", text);
        Assert.DoesNotContain("sk-test", text);
    }
}
