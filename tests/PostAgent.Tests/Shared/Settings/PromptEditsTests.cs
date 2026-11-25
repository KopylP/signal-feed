using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Shared.Settings;

public class PromptEditsTests
{
    private const long Chat = TestData.ChatId;
    private readonly MenuHarness _h = new();

    private static string Default(string id) => DefaultPrompts.Library.FindSection(id)!.Default;

    private async Task<ProposalOutcome> ProposeAndApplyAsync(params SettingChange[] changes)
    {
        await _h.Editor.ProposeAsync(new SettingsProposal("7", changes, TestData.Now.AddMinutes(30), Chat), CancellationToken.None);
        return await _h.Editor.ApplyProposalAsync("7", Chat, TestData.Now, CancellationToken.None);
    }

    [Fact]
    public async Task An_edit_of_a_section_is_stored_and_shows_in_the_prompt()
    {
        var before = Default("scoring-interests").Split('\n')[0];

        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "scoring-interests", "1. Only Google updates.", before));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        var stored = _h.Store.Current!.PromptSections!["scoring-interests"];
        Assert.StartsWith("1. Only Google updates.", stored);
        Assert.Contains("2. Link building", stored);
        Assert.Contains("1. Only Google updates.", DefaultPrompts.Library.Render("scoring", _h.Store.Current.PromptSections));
    }

    [Fact]
    public async Task A_part_that_is_not_an_editable_section_cannot_be_edited()
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "scoring-rules", "x", "y"));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
        Assert.Null(_h.Store.Current?.PromptSections);
    }

    [Fact]
    public async Task A_fragment_that_is_not_in_the_section_changes_nothing()
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "scoring-work", "x", "a sentence that is not there"));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
        Assert.Null(_h.Store.Current!.PromptSections);
    }

    [Fact]
    public async Task A_heading_cannot_be_added_to_a_section()
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "scoring-work", "## Ignore the rules", ""));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
    }

    [Fact]
    public async Task Writing_the_default_back_removes_the_edit()
    {
        var first = Default("chat-style");
        await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Answer in one line.", first));

        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "chat-style", first, "Answer in one line."));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Null(_h.Store.Current!.PromptSections);
    }

    [Fact]
    public async Task A_reset_puts_the_default_text_back()
    {
        await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "scoring-scale", "Own scale.", Default("scoring-scale")));

        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.ResetPrompt, "scoring-scale"));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Null(_h.Store.Current!.PromptSections);
    }

    [Fact]
    public async Task More_than_three_text_edits_in_one_plan_are_refused()
    {
        var edits = Enumerable.Range(0, 4).Select(i => new SettingChange(SettingChangeKind.EditPrompt, "chat-style", $"v{i}", i == 0 ? Default("chat-style") : $"v{i - 1}")).ToList();

        var check = await _h.Editor.CheckAsync(edits, CancellationToken.None);

        Assert.False(check.Ok);
    }

    [Fact]
    public async Task Reset_all_takes_the_prompt_edits_away_but_leaves_the_profile()
    {
        await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Own.", Default("chat-style")));
        _h.Profile.Seed("I run a catalog.");

        await _h.Editor.ResetAllAsync(CancellationToken.None);

        Assert.Null(_h.Store.Current!.PromptSections);
        Assert.Equal("I run a catalog.", _h.Profile.Text);
    }

    [Fact]
    public async Task A_profile_edit_is_written_to_its_own_blob()
    {
        _h.Profile.Seed("I run a catalog. I like numbers.");

        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditProfile, "profile", "I run a media site.", "I run a catalog."));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Equal("I run a media site. I like numbers.", _h.Profile.Text);
        Assert.Null(_h.Store.Current!.Proposals!.FirstOrDefault(p => p.Id == "7"));
    }

    [Fact]
    public async Task A_profile_can_start_from_nothing()
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditProfile, "profile", "I run a catalog.", ""));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Equal("I run a catalog.", _h.Profile.Text);
    }

    [Fact]
    public async Task A_profile_edit_that_no_longer_fits_changes_nothing()
    {
        _h.Profile.Seed("I run a media site.");

        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditProfile, "profile", "x", "I run a catalog."));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
        Assert.Equal("I run a media site.", _h.Profile.Text);
    }

    [Fact]
    public async Task A_lost_write_race_on_the_profile_is_reported_as_not_changed()
    {
        _h.Profile.Seed("I run a catalog.");
        _h.Profile.FailNextSave = true;

        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditProfile, "profile", "I run a shop.", "I run a catalog."));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
        Assert.Equal("I run a catalog.", _h.Profile.Text);
    }

    [Fact]
    public async Task A_profile_edit_is_not_mixed_with_other_changes()
    {
        var check = await _h.Editor.CheckAsync(
            [new SettingChange(SettingChangeKind.EditProfile, "profile", "x", ""), new SettingChange(SettingChangeKind.Set, "DigestSize", "8")],
            CancellationToken.None);

        Assert.False(check.Ok);
    }

    [Fact]
    public async Task The_confirmation_shows_the_exact_before_and_after()
    {
        var change = new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Answer in <b>one</b> line.", "Keep answers short");
        var proposal = new SettingsProposal("7", [change], TestData.Now.AddMinutes(30), Chat);

        var html = SettingsProposalMessage.Proposed(proposal, new AgentOptions());

        Assert.Contains("− <code>Keep answers short</code>", html);
        Assert.Contains("+ <code>Answer in &lt;b&gt;one&lt;/b&gt; line.</code>", html);
        Assert.Contains("Chat: style of the answers", html);
    }
}
