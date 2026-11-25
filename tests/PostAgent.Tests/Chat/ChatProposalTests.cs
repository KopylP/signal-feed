using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Chat;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat;

public class ChatProposalTests
{
    private readonly MenuHarness _h = new();
    private readonly InMemoryChatStateStore _state = new();
    private readonly RecordingNotifier _notifier = new();

    private ChatProcessor Processor(IReadOnlyList<SettingChange>? proposed) =>
        new(_state, new FakeChatAgent((_, _) => Task.FromResult(new ChatReply("Підготував план", FakeChatAgent.Session(), proposed))),
            _notifier, _h.ProposalSender, new FakeTimeProvider(Now), NullLogger<ChatProcessor>.Instance);

    private async Task Started() => await _state.StartNewAsync(new ReportSnapshot("r1", Now, "Огляд", [], []), CancellationToken.None);

    [Fact]
    public async Task A_proposal_is_sent_after_the_answer_with_buttons_for_its_update()
    {
        await Started();

        await Processor([new SettingChange(SettingChangeKind.Set, "DigestSize", "8")]).HandleAsync(new ChatRequest(77, "постав 8", "r1"), CancellationToken.None);

        Assert.Equal(["Підготував план"], _notifier.Sent);
        var sent = Assert.Single(_h.Panel.Sent);
        Assert.Equal(MenuHarness.BoundChat, sent.ChatId);
        Assert.Contains("<b>8</b>", sent.Html);
        Assert.Equal(["pa:77", "pn:77"], MenuHarness.Buttons(sent.Keyboard));
        Assert.Equal("77", Assert.Single(_h.Store.Current!.Proposals!).Id);
    }

    [Fact]
    public async Task An_answer_without_changes_sends_no_proposal()
    {
        await Started();

        await Processor(null).HandleAsync(new ChatRequest(77, "привіт", "r1"), CancellationToken.None);

        Assert.Empty(_h.Panel.Sent);
        Assert.Null(_h.Store.Current);
    }

    [Fact]
    public async Task A_redelivered_question_does_not_send_the_proposal_again()
    {
        await Started();
        var processor = Processor([new SettingChange(SettingChangeKind.Set, "DigestSize", "8")]);

        await processor.HandleAsync(new ChatRequest(77, "постав 8", "r1"), CancellationToken.None);
        await processor.HandleAsync(new ChatRequest(77, "постав 8", "r1"), CancellationToken.None);

        Assert.Single(_h.Panel.Sent);
    }
}
