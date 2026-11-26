using PostAgent.Shared.Settings;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat.Agent.Tools;

public class WatchToolsTests
{
    private readonly MenuHarness _h = new();
    private readonly List<SettingChange> _draft = [];

    private ToolShim Tools() => new(_h.ToolsFactory.Create(_draft, TestData.ChatId));

    [Fact]
    public async Task A_topic_goes_into_the_plan_cleaned_and_nothing_is_saved()
    {
        var answer = await Tools().ProposeAddWatch("  Google   core update ", CancellationToken.None);

        Assert.Equal([new SettingChange(SettingChangeKind.AddWatch, "Google core update")], _draft);
        Assert.Contains("Nothing is changed yet", answer);
        Assert.Null(_h.Store.Current);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ;; ")]
    public async Task An_empty_topic_is_refused(string phrase)
    {
        var answer = await Tools().ProposeAddWatch(phrase, CancellationToken.None);

        Assert.Contains("not a usable topic", answer);
        Assert.Empty(_draft);
    }

    [Fact]
    public async Task Applying_the_plan_stores_the_topic_and_the_agent_sees_it_in_the_settings()
    {
        await Tools().ProposeAddWatch("google core update", CancellationToken.None);
        await _h.Editor.ProposeAsync(new SettingsProposal("1", _draft, _h.Time.GetUtcNow() + SettingsProposal.Lifetime), CancellationToken.None);

        var outcome = await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, _h.Time.GetUtcNow(), CancellationToken.None);

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Equal(["google core update"], _h.Store.Current?.Watches);
        Assert.Contains("google core update", await Tools().GetSettings(CancellationToken.None));
    }

    [Fact]
    public async Task The_same_topic_in_another_case_is_not_added_twice()
    {
        _h.Store.Seed(SettingsOverrides.Empty with { Watches = ["google core update"] });
        await Tools().ProposeAddWatch("Google Core Update", CancellationToken.None);
        await _h.Editor.ProposeAsync(new SettingsProposal("1", _draft, _h.Time.GetUtcNow() + SettingsProposal.Lifetime), CancellationToken.None);

        await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, _h.Time.GetUtcNow(), CancellationToken.None);

        Assert.Single(_h.Store.Current!.Watches!);
    }

    [Fact]
    public async Task The_number_of_followed_topics_is_limited()
    {
        _h.Store.Seed(SettingsOverrides.Empty with { Watches = Enumerable.Range(1, WatchPhrases.MaxCount).Select(i => "topic" + i).ToList() });

        var answer = await Tools().ProposeAddWatch("one more", CancellationToken.None);

        Assert.StartsWith("Rejected", answer);
        Assert.Empty(_draft);
    }

    [Fact]
    public async Task A_followed_topic_can_be_removed_but_a_made_up_one_is_sent_back_to_the_list()
    {
        _h.Store.Seed(SettingsOverrides.Empty with { Watches = ["google core update", "ai overviews"] });

        var missing = await Tools().ProposeRemoveWatch("seo", CancellationToken.None);
        var removed = await Tools().ProposeRemoveWatch("AI Overviews", CancellationToken.None);

        Assert.Contains("not followed", missing);
        Assert.Contains("Nothing is changed yet", removed);
        Assert.Equal([new SettingChange(SettingChangeKind.RemoveWatch, "ai overviews")], _draft);
    }

    [Fact]
    public async Task Resetting_the_alerts_group_returns_the_topics_to_the_app_setting()
    {
        _h.Store.Seed(SettingsOverrides.Empty with { Watches = ["google core update"] });

        await _h.Editor.ProposeAsync(new SettingsProposal("1", [new SettingChange(SettingChangeKind.ResetGroup, nameof(SettingGroup.Alerts))], _h.Time.GetUtcNow() + SettingsProposal.Lifetime), CancellationToken.None);
        await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, _h.Time.GetUtcNow(), CancellationToken.None);

        Assert.Null(_h.Store.Current!.Watches);
    }

    [Fact]
    public async Task The_confirmation_text_names_the_topic_and_escapes_it()
    {
        var proposal = new SettingsProposal("1", [new SettingChange(SettingChangeKind.AddWatch, "<b>x</b>")], _h.Time.GetUtcNow() + SettingsProposal.Lifetime);

        var text = SettingsProposalMessage.Proposed(proposal, new AgentOptions());

        Assert.Contains("Стежити за темою", text);
        Assert.DoesNotContain("<b>x</b>", text);
    }

    [Fact]
    public void The_overrides_applied_on_top_set_the_followed_topics_of_the_options()
    {
        var applied = OverridesApplier.Apply(new AgentOptions { Watches = "old topic" }, SettingsOverrides.Empty with { Watches = ["a b", "c"] });

        Assert.Equal(["a b", "c"], applied.WatchList);
    }

    [Fact]
    public void The_topics_survive_the_overrides_json_and_an_old_blob_without_them_still_reads()
    {
        var written = SettingsOverridesJson.Write(SettingsOverrides.Empty with { Watches = ["a b"] });
        Assert.True(SettingsOverridesJson.TryRead(written, out var read));
        Assert.Equal(["a b"], read.Watches);

        Assert.True(SettingsOverridesJson.TryRead("{\"values\":{}}", out var old));
        Assert.Null(old.Watches);
    }
}
