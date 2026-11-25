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

    private LoginAttempt CurrentAttempt => Assert.IsType<LoginAttempt>(_attempts.Current);

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

    private static MemoryStream Body(
        string? initData = null, string attempt = AttemptId, string? code = "12345", string? phone = null,
        string? srpA = null, string? srpM1 = null, string? action = null) =>
        new(JsonSerializer.SerializeToUtf8Bytes(new { attempt, initData = initData ?? Sign(Owner, TestData.Now), action, phone, code, srpA, srpM1 }));

    private async Task<HttpStatusCode> Post(Stream body) => (await Form().HandleAsync(body, CancellationToken.None)).Status;

    [Fact]
    public async Task Hands_the_code_to_the_attempt_of_the_chat_that_opened_the_form()
    {
        Assert.Equal(HttpStatusCode.Accepted, await Post(Body()));

        Assert.Equal("12345", CurrentAttempt.Code);
    }

    [Fact]
    public async Task Hands_a_password_proof_to_the_attempt()
    {
        var a = Convert.ToBase64String(new byte[PasswordProof.ALength]);
        var m1 = Convert.ToBase64String(new byte[PasswordProof.M1Length]);

        Assert.Equal(HttpStatusCode.Accepted, await Post(Body(code: null, srpA: a, srpM1: m1)));

        Assert.Equal(PasswordProof.M1Length, CurrentAttempt.Proof?.M1.Length);
    }

    [Fact]
    public async Task Ignores_a_password_sent_in_plain_text()
    {
        var body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { attempt = AttemptId, initData = Sign(Owner, TestData.Now), password = "secret" }));

        Assert.Equal(HttpStatusCode.BadRequest, await Post(body));
        Assert.Null(CurrentAttempt.Proof);
    }

    [Fact]
    public async Task Gives_the_challenge_of_the_password_step_without_counting_a_submission()
    {
        var challenge = new PasswordChallenge(7, 3, new byte[256], [1], [2], [3]);
        _attempts.Current = CurrentAttempt with { Challenge = challenge };

        var result = await Form().HandleAsync(Body(code: null, action: LoginForm.ChallengeAction), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Same(challenge, result.Challenge);
        Assert.Equal(0, _attempts.Current.Submissions);
    }

    [Fact]
    public async Task Has_no_challenge_before_the_password_step()
    {
        Assert.Equal(HttpStatusCode.Conflict, await Post(Body(code: null, action: LoginForm.ChallengeAction)));
    }

    [Fact]
    public async Task Gives_no_challenge_to_another_user()
    {
        _attempts.Current = CurrentAttempt with { Challenge = new PasswordChallenge(7, 3, new byte[256], [1], [2], [3]) };
        _bindings.Bound.Add(7);

        var result = await Form().HandleAsync(Body(Sign(7, TestData.Now), code: null, action: LoginForm.ChallengeAction), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, result.Status);
        Assert.Null(result.Challenge);
    }

    [Fact]
    public async Task Hands_the_phone_to_the_attempt_in_one_form()
    {
        Assert.Equal(HttpStatusCode.Accepted, await Post(Body(code: null, phone: "+380 67 123 45 67")));

        Assert.Equal("+380671234567", CurrentAttempt.Phone);
    }

    [Fact]
    public async Task Rejects_data_not_signed_by_telegram_for_this_bot()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Post(Body(Sign(Owner, TestData.Now, "999:other"))));
        Assert.Null(CurrentAttempt.Code);
    }

    [Fact]
    public async Task Rejects_a_form_opened_by_another_user()
    {
        _bindings.Bound.Add(7);

        Assert.Equal(HttpStatusCode.Forbidden, await Post(Body(Sign(7, TestData.Now))));
        Assert.Null(CurrentAttempt.Code);
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
        _attempts.Current = CurrentAttempt with { Submissions = LoginAttempt.MaxSubmissions };

        Assert.Equal(HttpStatusCode.Conflict, await Post(Body()));
    }

    [Theory]
    [InlineData("12a45", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData(null, "not a phone")]
    public async Task Rejects_input_that_is_not_a_phone_a_code_or_a_proof(string? code, string? phone)
    {
        Assert.Equal(HttpStatusCode.BadRequest, await Post(Body(code: code, phone: phone)));
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
