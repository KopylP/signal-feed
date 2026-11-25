using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Login;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.Login.InitDataSigner;

namespace PostAgent.Tests.Login;

public class LoginFormTests
{
    private const long Owner = 42;
    private const string AttemptId = "attempt-0123456789abcdef0123456789abcdef";

    private readonly InMemoryLoginAttemptStore _attempts = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly InMemoryChatBindingStore _bindings = new(Owner);

    public LoginFormTests()
    {
        _attempts.Current = new LoginAttempt(AttemptId, Owner, TestData.Now.AddMinutes(5));
    }

    private LoginForm Form() => new(
        _attempts,
        new ChatResolver(Options.Create(new AgentOptions()), _bindings),
        Options.Create(new AgentOptions { TelegramBotToken = BotToken }),
        _time,
        NullLogger<LoginForm>.Instance);

    private static MemoryStream Body(string? initData = null, string attempt = AttemptId, string? code = "12345", string? password = null) =>
        new(JsonSerializer.SerializeToUtf8Bytes(new { attempt, initData = initData ?? Sign(Owner, TestData.Now), code, password }));

    private Task<HttpStatusCode> Post(Stream body) => Form().HandleAsync(body, CancellationToken.None);

    [Fact]
    public async Task Hands_the_code_and_password_to_the_attempt_of_the_chat_that_opened_the_form()
    {
        Assert.Equal(HttpStatusCode.Accepted, await Post(Body(password: "secret")));

        Assert.Equal("12345", _attempts.Current!.Code);
        Assert.Equal("secret", _attempts.Current.Password);
    }

    [Fact]
    public async Task Rejects_data_not_signed_by_telegram_for_this_bot()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Post(Body(Sign(Owner, TestData.Now, "999:other"))));
        Assert.Null(_attempts.Current!.Code);
    }

    [Fact]
    public async Task Rejects_a_form_opened_by_another_user()
    {
        _bindings.Bound.Add(7);

        Assert.Equal(HttpStatusCode.Forbidden, await Post(Body(Sign(7, TestData.Now))));
        Assert.Null(_attempts.Current!.Code);
    }

    [Fact]
    public async Task Rejects_a_form_with_another_attempt_id()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await Post(Body(attempt: "attempt-guess")));
    }

    [Fact]
    public async Task Rejects_a_chat_that_was_unbound_after_the_attempt_started()
    {
        _bindings.Bound.Clear();
        _bindings.Bound.Add(7);

        Assert.Equal(HttpStatusCode.Forbidden, await Post(Body()));
    }

    [Fact]
    public async Task Rejects_a_form_after_the_attempt_expired()
    {
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(HttpStatusCode.Forbidden, await Post(Body(Sign(Owner, _time.GetUtcNow()))));
    }

    [Fact]
    public async Task Rejects_a_form_when_no_sign_in_is_running()
    {
        _attempts.Current = null;

        Assert.Equal(HttpStatusCode.Forbidden, await Post(Body()));
    }

    [Fact]
    public async Task Stops_accepting_forms_after_the_submission_limit()
    {
        _attempts.Current = _attempts.Current! with { Submissions = LoginAttempt.MaxSubmissions };

        Assert.Equal(HttpStatusCode.Conflict, await Post(Body()));
    }

    [Theory]
    [InlineData("12a45", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task Rejects_input_that_is_not_a_code_or_a_password(string? code, string? password)
    {
        Assert.Equal(HttpStatusCode.BadRequest, await Post(Body(code: code, password: password)));
    }

    [Fact]
    public async Task Rejects_a_body_over_the_size_limit_without_parsing_it()
    {
        var body = new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', LoginForm.MaxBodyBytes + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, await Post(body));
    }

    [Fact]
    public async Task Rejects_a_body_that_is_not_json()
    {
        Assert.Equal(HttpStatusCode.BadRequest, await Post(new MemoryStream("code=12345"u8.ToArray())));
    }
}
