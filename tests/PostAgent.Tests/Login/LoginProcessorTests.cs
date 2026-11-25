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
    private const string Phone = "+380671234567";

    private readonly InMemoryLoginAttemptStore _attempts = new();
    private readonly FakeSessionLock _lock = new();
    private readonly RecordingSessionStore _sessions = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly RecordingPanel _panel = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private ScriptedUserLogin _login = new();
    private string _host = Host;
    private string _expectedPhone = string.Empty;

    private readonly LoginAttempt _attempt;

    public LoginProcessorTests()
    {
        _attempt = new LoginAttempt("attempt-1", Owner, TestData.Now + LoginInbox.AttemptLifetime);
        _attempts.Current = _attempt;
    }

    private LoginProcessor Processor()
    {
        var options = Options.Create(new AgentOptions { WebsiteHostName = _host, TelegramPhone = _expectedPhone });
        return new LoginProcessor(
            _attempts, new ScriptedUserLoginFactory(_login), _lock, _sessions, _notifier, _panel, options, _time, NullLogger<LoginProcessor>.Instance);
    }

    // The forms the owner sends, one per poll, starting with the phone.
    private void Forms(params LoginSubmission[] forms)
    {
        foreach (var form in forms)
        {
            _attempts.Queued.Enqueue(form);
        }
    }

    private static LoginSubmission PhoneForm(string phone = Phone) => new(phone, null, null);

    private static LoginSubmission CodeForm(string? code, string? password = null) => new(null, code, password);

    private List<string> Prompts => _panel.Sent.Select(m => m.Html).ToList();

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
    public async Task Asks_for_the_phone_then_for_the_code_and_saves_the_session()
    {
        Forms(PhoneForm(), CodeForm("12345"));

        await RunAsync();

        Assert.Equal([Phone], _login.Phones);
        Assert.Equal(2, _panel.Sent.Count);
        Assert.All(_panel.Sent, m => Assert.Equal(Owner, m.ChatId));
        Assert.Equal($"https://{Host}/api/telegram/login?a=attempt-1&s=phone", WebApp(0));
        Assert.Equal($"https://{Host}/api/telegram/login?a=attempt-1&s=code", WebApp(1));
        Assert.Contains("+380•••••••67", Prompts[1], StringComparison.Ordinal);
        Assert.DoesNotContain(Phone, Prompts[1], StringComparison.Ordinal);

        Assert.Equal([[1, 2, 3]], _sessions.Saved);
        Assert.Equal([LoginTexts.Done("reader")], _notifier.Replies);
        Assert.True(_login.Disposed);
        Assert.Equal(1, _lock.Released);
        Assert.Null(_attempts.Current);
    }

    private string? WebApp(int message) => Assert.Single(Assert.Single(_panel.Sent[message].Keyboard.Rows)).WebApp?.AbsoluteUri;

    [Fact]
    public async Task Ignores_a_code_sent_before_the_phone()
    {
        Forms(CodeForm("12345"), PhoneForm(), CodeForm("12345"));

        await RunAsync();

        Assert.Equal(["12345"], _login.Codes);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Signs_in_only_the_number_in_telegram_phone_when_it_is_set()
    {
        _expectedPhone = "+380 67 123 45 67";
        Forms(PhoneForm("+380501112233"), PhoneForm(), CodeForm("12345"));

        await RunAsync();

        Assert.Equal([Phone], _login.Phones);
        Assert.Contains(LoginTexts.PhoneMismatch, Prompts);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Gives_up_after_too_many_wrong_numbers()
    {
        _expectedPhone = Phone;
        Forms(PhoneForm("+380501112233"), PhoneForm("+380501112234"), PhoneForm("+380501112235"), PhoneForm());

        await RunAsync();

        Assert.Empty(_login.Phones);
        Assert.Equal([LoginTexts.TooManyTries], _notifier.Replies);
    }

    [Fact]
    public async Task Uses_the_password_sent_together_with_the_code()
    {
        _login = new ScriptedUserLogin { Password = "secret" };
        Forms(PhoneForm(), CodeForm("12345", "secret"));

        await RunAsync();

        Assert.Equal(["secret"], _login.Passwords);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Asks_for_the_password_when_only_the_code_came()
    {
        _login = new ScriptedUserLogin { Password = "secret" };
        Forms(PhoneForm(), CodeForm("12345"), CodeForm(null, "secret"));

        await RunAsync();

        Assert.Equal(["12345"], _login.Codes);
        Assert.Equal(LoginTexts.PasswordNeeded, Prompts[^1]);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Lets_the_owner_retry_a_wrong_code()
    {
        Forms(PhoneForm(), CodeForm("11111"), CodeForm("12345"));

        await RunAsync();

        Assert.Equal(LoginTexts.WrongCode, Prompts[^1]);
        Assert.Equal(LoginTexts.CodeButton, Assert.Single(Assert.Single(_panel.Sent[^1].Keyboard.Rows)).Text);
        Assert.Single(_sessions.Saved);
    }

    [Fact]
    public async Task Gives_up_after_too_many_wrong_codes_or_passwords()
    {
        _login = new ScriptedUserLogin { Password = "secret" };
        Forms(PhoneForm(), CodeForm("11111"), CodeForm("12345", "wrong"), CodeForm(null, "wrong again"), CodeForm(null, "secret"));

        await RunAsync();

        Assert.Equal([LoginTexts.TooManyTries], _notifier.Replies);
        Assert.Empty(_sessions.Saved);
        Assert.Null(_attempts.Current);
    }

    [Fact]
    public async Task Ends_without_a_session_when_the_attempt_expires()
    {
        await RunAsync();

        Assert.Equal([LoginTexts.Expired], _notifier.Replies);
        Assert.Empty(_login.Phones);
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
        _host = string.Empty;

        await RunAsync();

        Assert.Equal([LoginTexts.NoHost], _notifier.Replies);
        Assert.Empty(_panel.Sent);
    }

    [Fact]
    public async Task Points_to_tglogin_when_telegram_asks_for_something_the_bot_does_not_do()
    {
        _login = new ScriptedUserLogin { FirstStep = LoginStep.Unsupported };
        Forms(PhoneForm());

        await RunAsync();

        Assert.Equal([LoginTexts.Unsupported], _notifier.Replies);
    }

    [Fact]
    public async Task Reports_a_telegram_error_and_removes_the_attempt()
    {
        _login = new ScriptedUserLogin { StartFailure = new RpcException(400, "PHONE_NUMBER_INVALID") };
        Forms(PhoneForm());

        await RunAsync();

        Assert.Equal([LoginTexts.Failed("PHONE_NUMBER_INVALID")], _notifier.Replies);
        Assert.Empty(_sessions.Saved);
        Assert.Null(_attempts.Current);
        Assert.Equal(1, _lock.Released);
    }

    [Fact]
    public async Task Hides_the_text_of_an_unexpected_error()
    {
        _login = new ScriptedUserLogin { StartFailure = new InvalidOperationException("details that may be private") };
        Forms(PhoneForm());

        await RunAsync();

        Assert.Equal([LoginTexts.Failed(null)], _notifier.Replies);
    }
}
