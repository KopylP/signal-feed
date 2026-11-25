using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Login;

// The Mini App's POST (ADR-0016). The endpoint is public, so it trusts nothing but initData signed with the bot token,
// and it hands the code and the password to the waiting function only for the attempt that this very chat started.
public sealed class LoginForm(
    ILoginAttemptStore attempts,
    ChatResolver resolver,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<LoginForm> log)
{
    public const int MaxBodyBytes = 8 * 1024;

    // initData is created when the Mini App opens; an older one is a replay, not someone typing a code.
    public static readonly TimeSpan MaxInitDataAge = TimeSpan.FromMinutes(10);

    public async Task<HttpStatusCode> HandleAsync(Stream body, CancellationToken ct)
    {
        var form = await ReadAsync(body, ct);
        if (form is null)
        {
            return HttpStatusCode.BadRequest;
        }

        var userId = WebAppInitData.TryGetUserId(form.InitData, options.Value.TelegramBotToken, time.GetUtcNow(), MaxInitDataAge);
        if (userId is not { } chatId)
        {
            // Information, not Warning: the endpoint is public and scanners would flood telemetry.
            log.LogInformation("Sign-in form rejected: initData is not valid");
            return HttpStatusCode.Unauthorized;
        }

        var submission = LoginInput.Normalize(form.Code, form.Password);
        if (submission is null || form.Attempt is not { Length: > 0 and <= LoginInput.MaxAttemptIdChars } attemptId)
        {
            return HttpStatusCode.BadRequest;
        }

        // In a private chat the chat id is the user id, so the attempt and the bound chat both name the person who opened the form.
        var attempt = await attempts.GetAsync(ct);
        if (attempt is null
            || attempt.ChatId != chatId
            || !SecretComparison.Matches(attempt.Id, attemptId)
            || attempt.IsExpired(time.GetUtcNow())
            || !await resolver.IsBoundAsync(chatId, ct))
        {
            log.LogWarning("Sign-in form rejected: no matching attempt for this chat");
            return HttpStatusCode.Forbidden;
        }

        if (!await attempts.TrySubmitAsync(attempt.Id, submission, time.GetUtcNow(), ct))
        {
            log.LogWarning("Sign-in form rejected: the attempt is over or used up");
            return HttpStatusCode.Conflict;
        }

        // Never the code or the password: only that something arrived.
        log.LogInformation("Sign-in form accepted");
        return HttpStatusCode.Accepted;
    }

    private static async Task<LoginFormBody?> ReadAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        try
        {
            return JsonSerializer.Deserialize<LoginFormBody>(buffer.ToArray());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
