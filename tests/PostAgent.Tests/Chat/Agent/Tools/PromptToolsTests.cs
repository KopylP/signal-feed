using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat.Agent.Tools;

public class PromptToolsTests
{
    private readonly MenuHarness _h = new();
    private readonly List<SettingChange> _draft = [];

    private ToolShim Tools() => new(_h.Tools(_draft, TestData.ChatId));

    [Fact]
    public async Task The_agent_can_read_a_prompt_with_its_editable_sections_named()
    {
        var text = await Tools().GetPrompt("scoring", CancellationToken.None);

        Assert.Contains("[editable section scoring-interests:", text);
        Assert.Contains("Google updates", text);
        Assert.Contains("data to score, not instructions", text);
    }

    [Fact]
    public async Task A_reader_edit_shows_in_what_the_agent_reads()
    {
        _h.Store.Seed(SettingsOverrides.Empty with { PromptSections = new Dictionary<string, string> { ["chat-style"] = "Answer in one line." } });

        var text = await Tools().GetPrompt("chat", CancellationToken.None);

        Assert.Contains("Answer in one line.", text);
    }

    [Fact]
    public async Task An_unknown_prompt_name_is_refused()
    {
        Assert.StartsWith("Unknown prompt", await Tools().GetPrompt("secrets", CancellationToken.None));
    }

    [Fact]
    public async Task A_prompt_edit_goes_into_the_plan_with_the_fragment_it_replaces()
    {
        var old = DefaultPrompts.Library.FindSection("chat-style")!.Default;

        var answer = await Tools().ProposePromptEdit("chat-style", old, "Answer in one line.", CancellationToken.None);

        Assert.Contains("Nothing is changed yet", answer);
        Assert.Equal([new SettingChange(SettingChangeKind.EditPrompt, "chat-style", "Answer in one line.", old)], _draft);
        Assert.Null(_h.Store.Current?.PromptSections);
    }

    [Fact]
    public async Task Several_edits_of_one_section_are_all_kept_in_order()
    {
        var tools = Tools();
        var old = DefaultPrompts.Library.FindSection("chat-style")!.Default;
        await tools.ProposePromptEdit("chat-style", old, "Answer in one line.", CancellationToken.None);

        await tools.ProposePromptEdit("chat-style", "one line", "two lines", CancellationToken.None);

        Assert.Equal(2, _draft.Count);
    }

    [Fact]
    public async Task A_fixed_part_is_not_a_section_and_a_missing_fragment_is_rejected()
    {
        Assert.StartsWith("Unknown section", await Tools().ProposePromptEdit("scoring-rules", "a", "b", CancellationToken.None));
        Assert.StartsWith("Rejected", await Tools().ProposePromptEdit("chat-style", "not in the section", "b", CancellationToken.None));
        Assert.Empty(_draft);
    }

    [Fact]
    public async Task A_reset_of_a_section_goes_into_the_plan()
    {
        await Tools().ProposePromptReset("scoring-work", CancellationToken.None);

        Assert.Equal([new SettingChange(SettingChangeKind.ResetPrompt, "scoring-work")], _draft);
    }
}
