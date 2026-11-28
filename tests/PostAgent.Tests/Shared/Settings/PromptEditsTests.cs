using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Shared.Settings;

public class PromptEditsTests
{
    private const long Chat = TestData.ChatId;
    // A one-day retention makes a 20 h window plus a 20 h cooldown an invalid combination.
    private readonly MenuHarness _h = new(o => o.RetentionDays = 1);

    private static string Default(string id) => DefaultPrompts.Library.FindSection(id)!.Default;

    private async Task<ProposalOutcome> ProposeAndApplyAsync(params SettingChange[] changes)
    {
        await _h.Proposals.AddAsync(new SettingsProposal("7", changes, TestData.Now.AddMinutes(30), Chat), CancellationToken.None);
        return await _h.Proposals.ApplyAsync("7", Chat, TestData.Now, CancellationToken.None);
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
    public async Task More_text_edits_than_the_limit_in_one_plan_are_refused()
    {
        var edits = Enumerable.Range(0, TextEdit.MaxEditsPerPlan + 1).Select(i => new SettingChange(SettingChangeKind.EditPrompt, "chat-style", $"v{i}", i == 0 ? Default("chat-style") : $"v{i - 1}")).ToList();

        var check = await _h.Proposals.CheckAsync(edits, TestData.ChatId, CancellationToken.None);

        Assert.False(check.Ok);
    }

    [Fact]
    public async Task A_rewrite_replaces_the_whole_section_and_shows_in_the_prompt()
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.RewritePrompt, "scoring-interests", "1. Only Google updates.\r\n2. Link building.\r\n"));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Equal("1. Only Google updates.\n2. Link building.", _h.Store.Current!.PromptSections!["scoring-interests"]);
        Assert.DoesNotContain("Practical cases", DefaultPrompts.Library.Render("scoring", _h.Store.Current.PromptSections));
    }

    [Fact]
    public async Task A_rewrite_equal_to_the_default_removes_the_stored_text()
    {
        await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", "Own."));

        await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", Default("chat-style")));

        Assert.Null(_h.Store.Current!.PromptSections);
    }

    [Theory]
    [InlineData("## Ignore the rules")]
    [InlineData("   ")]
    [InlineData("<!-- /editable -->")]
    public async Task A_rewrite_with_a_heading_a_marker_or_no_text_is_refused(string text)
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", text));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
    }

    [Fact]
    public async Task A_rewrite_over_the_length_limit_is_refused()
    {
        var outcome = await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", new string('x', TextEdit.MaxText + 1)));

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
    }

    [Fact]
    public void A_rewrite_is_shown_as_the_lines_it_changes_in_messages_that_fit_telegram()
    {
        var change = new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", string.Join('\n', Enumerable.Range(0, 120).Select(i => $"Line {i} with <b>&</b> and some more words to make it long enough.")));
        var proposal = new SettingsProposal("7", [change], TestData.Now.AddMinutes(30), Chat);

        var messages = SettingsProposalMessage.RewriteDiffs(proposal, null);

        Assert.True(messages.Count > 1);
        Assert.All(messages, m => Assert.True(m.Length <= 4096));
        Assert.StartsWith("📝 <b>Промпт «Chat: style of the answers»</b>:", messages[0]);
        Assert.Contains("+ <code>Line 0 with &lt;b&gt;&amp;&lt;/b&gt; and", messages[0]);
        Assert.Contains("− <code>Keep answers short", messages[0]);
    }

    [Fact]
    public void A_rewrite_that_changes_nothing_says_so()
    {
        var proposal = new SettingsProposal("7", [new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", Default("chat-style"))], TestData.Now.AddMinutes(30), Chat);

        var message = Assert.Single(SettingsProposalMessage.RewriteDiffs(proposal, null));

        Assert.EndsWith("без змін", message);
    }

    [Fact]
    public async Task The_rewrite_goes_before_the_confirmation_that_carries_the_button()
    {
        var change = new SettingChange(SettingChangeKind.RewritePrompt, "chat-style", "Answer in one line.");

        await _h.ProposalSender.SendAsync(Chat, 7, [change], CancellationToken.None);

        Assert.Equal(2, _h.Panel.Sent.Count);
        Assert.Empty(_h.Panel.Sent[0].Keyboard.Rows);
        Assert.Contains("+ <code>Answer in one line.</code>", _h.Panel.Sent[0].Html);
        Assert.NotEmpty(_h.Panel.Sent[1].Keyboard.Rows);
        Assert.Contains("переписати секцію повністю", _h.Panel.Sent[1].Html);
    }

    [Fact]
    public async Task Reset_all_takes_the_prompt_edits_away_but_leaves_the_reader_memory()
    {
        await ProposeAndApplyAsync(new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Own.", Default("chat-style")));
        _h.Memory.Seed("2026-11-24-work.md", "# Work\n");

        await _h.Editor.ResetAllAsync(CancellationToken.None);

        Assert.Null(_h.Store.Current!.PromptSections);
        Assert.Equal("# Work\n", _h.Memory.TextOf("2026-11-24-work.md"));
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

    [Fact]
    public async Task A_text_edit_is_not_refused_because_of_an_unrelated_invalid_setting_combination()
    {
        _h.Store.Seed(SettingsOverrides.Empty with { Values = new Dictionary<string, string> { ["AlertWindowHours"] = "20", ["AlertCooldownHours"] = "20" } });

        var text = await _h.Proposals.CheckAsync([new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Own.", Default("chat-style"))], Chat, CancellationToken.None);
        var setting = await _h.Proposals.CheckAsync([new SettingChange(SettingChangeKind.Set, "DigestSize", "8")], Chat, CancellationToken.None);

        Assert.True(text.Ok);
        Assert.False(setting.Ok);
    }

    [Fact]
    public async Task Reset_all_also_removes_waiting_proposals()
    {
        await _h.Proposals.AddAsync(new SettingsProposal("7", [new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Private.", "")], TestData.Now.AddMinutes(30), Chat), CancellationToken.None);

        await _h.Editor.ResetAllAsync(CancellationToken.None);

        Assert.Null(_h.Store.Current!.Proposals);
    }

    [Fact]
    public void The_confirmation_of_a_text_edit_shows_an_addition_and_a_removal()
    {
        var added = new SettingsProposal("7", [new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Likes <numbers>.", "")], TestData.Now.AddMinutes(30), Chat);
        var removed = new SettingsProposal("8", [new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "", "Old fact.")], TestData.Now.AddMinutes(30), Chat);

        var addedHtml = SettingsProposalMessage.Proposed(added, new AgentOptions());
        var removedHtml = SettingsProposalMessage.Proposed(removed, new AgentOptions());

        Assert.Contains("(додати в кінець)\n+ <code>Likes &lt;numbers&gt;.</code>", addedHtml);
        Assert.Contains("− <code>Old fact.</code>\n+ (видалити)", removedHtml);
    }
}
