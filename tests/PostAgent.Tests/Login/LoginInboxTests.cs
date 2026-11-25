using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Login;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Tests.Login;

public class LoginInboxTests
{
    private const long Owner = MenuHarness.BoundChat;

    private readonly InMemoryLoginAttemptStore _attempts = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private RecordingLoginQueue _queue = new();

    private LoginAttempt CurrentAttempt => Assert.IsType<LoginAttempt>(_attempts.Current);

    private LoginInbox Inbox()
    {
        var resolver = new ChatResolver(Options.Create(new AgentOptions()), new InMemoryChatBindingStore(Owner));
        return new LoginInbox(resolver, _attempts, _queue, _notifier, _time, NullLogger<LoginInbox>.Instance);
    }

    [Fact]
    public async Task Starts_an_attempt_and_queues_the_sign_in_for_a_bound_private_chat()
    {
        Assert.True(await Inbox().HandleAsync(MenuHarness.Text("/login"), CancellationToken.None));

        var attempt = Assert.IsType<LoginAttempt>(_attempts.Current);
        Assert.Equal(Owner, attempt.ChatId);
        Assert.Equal(TestData.Now + LoginInbox.AttemptLifetime, attempt.ExpiresAt);
        Assert.True(attempt.Id.Length >= 40);
        Assert.Equal([new LoginRequest(attempt.Id, Owner)], _queue.Enqueued);
        Assert.Equal([LoginTexts.Starting], _notifier.Replies);
    }

    [Fact]
    public async Task Gives_every_attempt_a_new_id()
    {
        await Inbox().HandleAsync(MenuHarness.Text("/login"), CancellationToken.None);
        var first = CurrentAttempt.Id;
        _time.Advance(LoginInbox.AttemptLifetime);

        await Inbox().HandleAsync(MenuHarness.Text("/login"), CancellationToken.None);

        Assert.NotEqual(first, CurrentAttempt.Id);
    }

    [Theory]
    [InlineData(99, ChatType.Private)]
    [InlineData(Owner, ChatType.Group)]
    public async Task Says_nothing_to_a_chat_that_may_not_sign_in(long chatId, ChatType type)
    {
        Assert.True(await Inbox().HandleAsync(MenuHarness.Text("/login", chatId: chatId, type: type), CancellationToken.None));

        Assert.Null(_attempts.Current);
        Assert.Empty(_queue.Enqueued);
        Assert.Empty(_notifier.Replies);
    }

    [Fact]
    public async Task Refuses_a_second_sign_in_while_one_is_running()
    {
        await Inbox().HandleAsync(MenuHarness.Text("/login"), CancellationToken.None);
        var first = _attempts.Current;

        await Inbox().HandleAsync(MenuHarness.Text("/login", updateId: 2), CancellationToken.None);

        Assert.Same(first, _attempts.Current);
        Assert.Single(_queue.Enqueued);
        Assert.Equal(LoginTexts.InProgress, _notifier.Replies[^1]);
    }

    [Fact]
    public async Task Leaves_other_messages_to_the_next_handlers()
    {
        Assert.False(await Inbox().HandleAsync(MenuHarness.Text("/settings"), CancellationToken.None));
        Assert.False(await Inbox().HandleAsync(MenuHarness.Callback("x"), CancellationToken.None));
    }

    [Fact]
    public async Task Removes_the_attempt_when_it_cannot_be_queued()
    {
        _queue = new RecordingLoginQueue { Failure = new InvalidOperationException("queue down") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Inbox().HandleAsync(MenuHarness.Text("/login"), CancellationToken.None));

        Assert.Null(_attempts.Current);
    }
}
