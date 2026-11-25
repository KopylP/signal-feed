using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Login;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using TL;

namespace PostAgent.Tests.Login;

public class LoginProcessorTests
{
    private const long Owner = 42;
    private const string Host = "func-signal.azurewebsites.net";

    private readonly InMemoryLoginAttemptStore _attempts = new();
    private readonly FakeSessionLock _lock = new();
    private readonly RecordingSessionStore _sessions = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly RecordingPanel _panel = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private ScriptedUserLogin _login = new();
    private string? _host = Host;

    private readonly LoginAttempt _attempt;

    public LoginProcessorTests()
    {
        _attempt = new LoginAttempt("attempt-1", Owner, TestData.Now + LoginInbox.AttemptLifetime);
        _attempts.Current = _attempt;
    }

    private LoginProcessor Processor()
    {
        var options = Options.Create(new AgentOptions { WebsiteHostName = _host ?? string.Empty });
        return new LoginProcessor(
            _attempts, new ScriptedUserLoginFactory(_login), _lock, _sessions, _notifier, _panel, options, _time, NullLogger<LoginProcessor>.Instance);
    }

    // The processor waits on the fake clock; move it on until the run is over.
    private async Task RunAsync(LoginRequest? request = null)
    {
        var run = Processor().RunAsync(request ?? new LoginRequest(_attempt.Id, Owner), CancellationToken.None);
        for (var i = 0; i < 5000 && !run.IsCompleted; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1);
        }

        await run;
    }

    [Fact]
    public async Task Sends_the_mini_app_button_and_saves_the_session_once_the_code_arrives()
    {
        _attempts.Queued.Enqueue(new LoginSubmission("12345", null));

        await RunAsync();

        var (chatId, _, html, keyboard) = Assert.Single(_panel.Sent);
        Assert.Equal(Owner, chatId);
        Assert.Contains("Telegram", html, StringComparison.Ordinal);
        var button = Assert.Single(Assert.Single(keyboard.Rows));
        Assert.Equal($"https://{Host}/api/telegram/login?a=attempt-1", button.WebApp?.AbsoluteUri);

        Assert.Equal([[1, 2, 3]], _sessions.Saved);
        Assert.Equal([LoginTexts.Done("reader")], _notifier.Replies);
        Assert.True(_login.Disposed);
        Assert.Equal(1, _lock.Released);
        Assert.Null(_attempts.Current);
    }

    [Fact]
    public async Task Uses_the_password_sent_together_with_the_code()
    {
        _login = new ScriptedUserLogin { Password = "secret" };
        _attempts.Queued.Enqueue(new LoginSubmission("12345", "secret"));

        await RunAsync();

        Assert.Equal(["secret"], _login.Passwords);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Asks_for_the_password_when_only_the_code_came()
    {
        _login = new ScriptedUserLogin { Password = "secret" };
        _attempts.Queued.Enqueue(new LoginSubmission("12345", null));
        _attempts.Queued.Enqueue(new LoginSubmission(null, "secret"));

        await RunAsync();

        Assert.Equal(["12345"], _login.Codes);
        Assert.Equal([LoginTexts.PasswordNeeded, LoginTexts.Done("reader")], _notifier.Replies);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Lets_the_owner_retry_a_wrong_code()
    {
        _attempts.Queued.Enqueue(new LoginSubmission("11111", null));
        _attempts.Queued.Enqueue(new LoginSubmission("12345", null));

        await RunAsync();

        Assert.Equal([LoginTexts.WrongCode, LoginTexts.Done("reader")], _notifier.Replies);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Gives_up_after_too_many_wrong_codes_or_passwords()
    {
        _login = new ScriptedUserLogin { Password = "secret" };
        _attempts.Queued.Enqueue(new LoginSubmission("11111", null));
        _attempts.Queued.Enqueue(new LoginSubmission("12345", "wrong"));
        _attempts.Queued.Enqueue(new LoginSubmission(null, "wrong again"));
        _attempts.Queued.Enqueue(new LoginSubmission(null, "secret"));

        await RunAsync();

        Assert.Equal(LoginTexts.TooManyTries, _notifier.Replies[^1]);
        Assert.Empty(_sessions.Saved);
        Assert.Null(_attempts.Current);
    }

    [Fact]
    public async Task Ends_without_a_session_when_the_attempt_expires()
    {
        await RunAsync();

        Assert.Equal([LoginTexts.Expired], _notifier.Replies);
        Assert.Empty(_sessions.Saved);
        Assert.Equal(1, _lock.Released);
        Assert.Null(_attempts.Current);
    }

    [Fact]
    public async Task Tells_the_owner_when_the_session_stays_locked_by_a_collection()
    {
        _lock.Busy = true;

        await RunAsync();

        Assert.Equal([LoginTexts.Busy], _notifier.Replies);
        Assert.Empty(_panel.Sent);
        Assert.True(_time.GetUtcNow() - TestData.Now >= LoginProcessor.LockWait);
        Assert.Null(_attempts.Current);
    }

    [Fact]
    public async Task Does_nothing_for_an_attempt_that_is_not_the_current_one()
    {
        await RunAsync(new LoginRequest("attempt-old", Owner));

        Assert.Empty(_notifier.Replies);
        Assert.Equal(0, _lock.Acquired);
        Assert.Same(_attempt, _attempts.Current);
    }

    [Fact]
    public async Task Does_nothing_for_an_attempt_that_already_expired()
    {
        _time.Advance(LoginInbox.AttemptLifetime);

        await RunAsync();

        Assert.Empty(_notifier.Replies);
        Assert.Equal(0, _lock.Acquired);
    }

    [Fact]
    public async Task Points_to_tglogin_when_the_function_app_has_no_host_name()
    {
        _host = null;

        await RunAsync();

        Assert.Equal([LoginTexts.NoHost], _notifier.Replies);
        Assert.Empty(_panel.Sent);
    }

    [Fact]
    public async Task Points_to_tglogin_when_telegram_asks_for_something_the_bot_does_not_do()
    {
        _login = new ScriptedUserLogin { FirstStep = LoginStep.Unsupported };

        await RunAsync();

        Assert.Equal([LoginTexts.Unsupported], _notifier.Replies);
    }

    [Fact]
    public async Task Reports_a_telegram_error_and_removes_the_attempt()
    {
        _login = new ScriptedUserLogin { StartFailure = new RpcException(420, "FLOOD_WAIT_X", 60) };

        await RunAsync();

        Assert.Equal([LoginTexts.Failed("FLOOD_WAIT_X")], _notifier.Replies);
        Assert.Empty(_sessions.Saved);
        Assert.Null(_attempts.Current);
        Assert.Equal(1, _lock.Released);
    }

    [Fact]
    public async Task Hides_the_text_of_an_unexpected_error()
    {
        _login = new ScriptedUserLogin { StartFailure = new InvalidOperationException("details that may be private") };

        await RunAsync();

        Assert.Equal([LoginTexts.Failed(null)], _notifier.Replies);
    }
}
