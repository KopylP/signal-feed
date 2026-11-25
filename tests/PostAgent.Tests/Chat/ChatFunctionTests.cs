using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Chat;
using PostAgent.Shared.Chat;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat;

public class ChatFunctionTests
{
    private readonly InMemoryChatStateStore _state = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeChatAgent _agent = new(_ => "відповідь");

    private ChatFunction Create() =>
        new(new ChatProcessor(_state, _agent, _notifier, new PostAgent.Tests.SettingsMenu.MenuHarness().ProposalSender, new FakeTimeProvider(Now), NullLogger<ChatProcessor>.Instance),
            NullLogger<ChatFunction>.Instance);

    [Fact]
    public async Task A_valid_message_is_answered()
    {
        await _state.StartNewAsync(TestData.ChatId, new ReportSnapshot("r1", Now, null, [], []), CancellationToken.None);

        await Create().Run(new ChatRequest(1, "питання", "r1", TestData.ChatId).ToMessage(), CancellationToken.None);

        Assert.Equal(["відповідь"], _notifier.Replies);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task A_malformed_message_is_dropped_without_throwing_so_it_is_not_retried(string message)
    {
        await Create().Run(message, CancellationToken.None);

        Assert.Equal(0, _agent.Calls);
        Assert.Empty(_notifier.Replies);
    }
}
