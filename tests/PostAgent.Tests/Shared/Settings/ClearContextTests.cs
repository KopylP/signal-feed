using PostAgent.Chat.Agent.Tools;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Shared.Settings;

public class ClearContextTests
{
    private const long Chat = TestData.ChatId;
    private readonly MenuHarness _h = new();

    private async Task SeedAsync(long chatId)
    {
        await _h.ChatState.StartNewAsync(chatId, new ReportSnapshot("r1", TestData.Now, "Огляд", [], []), CancellationToken.None);
        var handle = (await _h.ChatState.LoadAsync(chatId, CancellationToken.None))!;
        await _h.ChatState.TrySaveAsync(chatId, handle, handle.State.WithProcessed(3, FakeChatAgent.Session()), CancellationToken.None);
    }

    private async Task<ProposalOutcome> ApplyAsync(params SettingChange[] changes)
    {
        await _h.Editor.ProposeAsync(new SettingsProposal("7", changes, TestData.Now.AddMinutes(30), Chat), CancellationToken.None);
        return await _h.Editor.ApplyProposalAsync("7", Chat, TestData.Now, CancellationToken.None);
    }

    [Fact]
    public async Task Nothing_is_cleared_until_the_owner_taps_apply()
    {
        await SeedAsync(Chat);
        var draft = new List<SettingChange>();

        var answer = await new PostAgent.Tests.Chat.Agent.Tools.ToolShim(_h.ToolsFactory.Create(draft, Chat)).ProposeClearContext(CancellationToken.None);

        Assert.Contains("Nothing is changed yet", answer);
        Assert.Equal([new SettingChange(SettingChangeKind.ClearContext, "context")], draft);
        Assert.NotNull(_h.ChatState.Of(Chat)!.Session);
    }

    [Fact]
    public async Task Apply_forgets_the_conversation_of_that_chat_only()
    {
        await SeedAsync(Chat);
        await SeedAsync(77);

        var outcome = await ApplyAsync(new SettingChange(SettingChangeKind.ClearContext, "context"));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Null(_h.ChatState.Of(Chat)!.Session);
        Assert.Equal("Огляд", _h.ChatState.Of(Chat)!.Report.Overview);
        Assert.NotNull(_h.ChatState.Of(77)!.Session);
    }

    [Fact]
    public async Task A_second_tap_changes_nothing_more()
    {
        await SeedAsync(Chat);
        await ApplyAsync(new SettingChange(SettingChangeKind.ClearContext, "context"));
        await SeedAsync(Chat);

        var second = await _h.Editor.ApplyProposalAsync("7", Chat, TestData.Now, CancellationToken.None);

        Assert.Equal(ProposalStatus.Gone, second.Status);
        Assert.NotNull(_h.ChatState.Of(Chat)!.Session);
    }

    [Fact]
    public async Task It_can_go_in_one_plan_with_a_setting()
    {
        await SeedAsync(Chat);

        var outcome = await ApplyAsync(new SettingChange(SettingChangeKind.Set, "DigestSize", "8"), new SettingChange(SettingChangeKind.ClearContext, "context"));

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Equal("8", _h.Store.Current!.Values["DigestSize"]);
        Assert.Null(_h.ChatState.Of(Chat)!.Session);
    }

    [Fact]
    public void The_confirmation_names_the_action_and_says_the_digest_stays()
    {
        var proposal = new SettingsProposal("7", [new SettingChange(SettingChangeKind.ClearContext, "context")], TestData.Now.AddMinutes(30), Chat);

        var html = SettingsProposalMessage.Proposed(proposal, new AgentOptions());

        Assert.Contains("Очистити контекст розмови", html);
        Assert.Contains("дайджест лишається", html);
    }

    [Fact]
    public void The_confirmation_buttons_are_green_for_apply_and_red_for_cancel()
    {
        var proposal = new SettingsProposal("7", [new SettingChange(SettingChangeKind.ClearContext, "context")], TestData.Now.AddMinutes(30), Chat);

        var buttons = SettingsProposalMessage.Keyboard(proposal).Rows.Single();

        Assert.Equal([PostAgent.Shared.Notifications.ButtonStyle.Success, PostAgent.Shared.Notifications.ButtonStyle.Danger], buttons.Select(b => b.Style));
    }
}
