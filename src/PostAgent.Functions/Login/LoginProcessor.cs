using Microsoft.Extensions.Options;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Shared.TelegramSession;
using RpcException = TL.RpcException;

namespace PostAgent.Login;

// The waiting side of a sign-in: holds one MTProto connection, sends the Mini App button and waits for what the owner types into it.
// It never throws: a queue retry would ask Telegram for another code in the middle of the owner's attempt.
public sealed class LoginProcessor(
    ILoginAttemptStore attempts,
    IUserLoginFactory logins,
    ISessionLock sessionLock,
    ISessionStore sessions,
    ITelegramNotifier notifier,
    ITelegramPanel panel,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<LoginProcessor> log)
{
    public const int MaxFailures = 3;
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan LockWait = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LockRetry = TimeSpan.FromSeconds(5);

    public async Task RunAsync(LoginRequest request, CancellationToken ct)
    {
        var attempt = await attempts.GetAsync(ct);
        if (attempt is null || attempt.Id != request.AttemptId || attempt.ChatId != request.ChatId || attempt.IsExpired(time.GetUtcNow()))
        {
            log.LogInformation("Sign-in dropped: its attempt is gone or expired");
            return;
        }

        try
        {
            await using var held = await AcquireLockAsync(attempt, ct);
            if (held is null)
            {
                await notifier.SendToAsync(attempt.ChatId, LoginTexts.Busy, ct);
                return;
            }

            await using var login = logins.Create();
            await notifier.SendToAsync(attempt.ChatId, await SignInAsync(login, attempt, ct), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An RPC error name (PHONE_NUMBER_BANNED, FLOOD_WAIT_60) helps the owner and holds nothing secret; other messages may.
            log.LogError(ex, "Sign-in failed");
            await TrySendAsync(attempt.ChatId, LoginTexts.Failed(ex is RpcException rpc ? rpc.Message : null), ct);
        }
        finally
        {
            await TryDeleteAsync(attempt.Id);
        }
    }

    // Returns the message that ends the sign-in.
    private async Task<string> SignInAsync(IUserLogin login, LoginAttempt attempt, CancellationToken ct)
    {
        var step = await login.StartAsync(ct);
        if (step != LoginStep.CodeRequired)
        {
            return step == LoginStep.Done ? await SaveAsync(login, ct) : LoginTexts.Unsupported;
        }

        var url = LoginPageUrl.Build(options.Value.WebsiteHostName, attempt.Id);
        if (url is null)
        {
            return LoginTexts.NoHost;
        }

        var keyboard = new InlineKeyboard([[KeyboardButton.OpenWebApp(LoginTexts.Button, url, ButtonStyle.Primary)]]);
        await panel.SendAsync(attempt.ChatId, LoginTexts.CodeSent((int)LoginInbox.AttemptLifetime.TotalMinutes), keyboard, ct);
        log.LogInformation("Sign-in code requested");

        var failures = 0;
        while (!attempt.IsExpired(time.GetUtcNow()))
        {
            var submission = await attempts.TakeSubmissionAsync(attempt.Id, ct);
            if (submission is null)
            {
                await Task.Delay(PollInterval, time, ct);
                continue;
            }

            step = await ApplyAsync(login, step, submission, ct);
            switch (step)
            {
                case LoginStep.Done:
                    return await SaveAsync(login, ct);
                case LoginStep.Unsupported:
                    return LoginTexts.Unsupported;
                case LoginStep.CodeInvalid or LoginStep.PasswordInvalid when ++failures >= MaxFailures:
                    return LoginTexts.TooManyTries;
                case LoginStep.CodeInvalid:
                    await notifier.SendToAsync(attempt.ChatId, LoginTexts.WrongCode, ct);
                    step = LoginStep.CodeRequired;
                    break;
                case LoginStep.PasswordInvalid:
                    await notifier.SendToAsync(attempt.ChatId, LoginTexts.WrongPassword, ct);
                    step = LoginStep.PasswordRequired;
                    break;
                case LoginStep.PasswordRequired when submission.Password is null:
                    await notifier.SendToAsync(attempt.ChatId, LoginTexts.PasswordNeeded, ct);
                    break;
            }
        }

        log.LogInformation("Sign-in expired before it was finished");
        return LoginTexts.Expired;
    }

    // A form may carry the code and the password together: the password is used as soon as Telegram asks for it.
    private static async Task<LoginStep> ApplyAsync(IUserLogin login, LoginStep step, LoginSubmission submission, CancellationToken ct)
    {
        if (step == LoginStep.CodeRequired)
        {
            if (submission.Code is null)
            {
                return step;
            }

            step = await login.SubmitCodeAsync(submission.Code, ct);
        }

        return step == LoginStep.PasswordRequired && submission.Password is { } password
            ? await login.SubmitPasswordAsync(password, ct)
            : step;
    }

    private async Task<string> SaveAsync(IUserLogin login, CancellationToken ct)
    {
        var account = login.AccountName;
        await sessions.SaveAsync(await login.ExportSessionAsync(ct), ct);
        log.LogInformation("Sign-in finished, the Telegram session was replaced");
        return LoginTexts.Done(account);
    }

    // A collection holds the lock for a minute or two; the sign-in waits for it rather than asking the owner to retry.
    private async Task<IAsyncDisposable?> AcquireLockAsync(LoginAttempt attempt, CancellationToken ct)
    {
        var giveUp = time.GetUtcNow() + LockWait;
        while (true)
        {
            if (await sessionLock.TryAcquireAsync(ct) is { } held)
            {
                return held;
            }

            if (time.GetUtcNow() >= giveUp || attempt.IsExpired(time.GetUtcNow()))
            {
                log.LogWarning("Sign-in gave up: the Telegram session stayed locked");
                return null;
            }

            await Task.Delay(LockRetry, time, ct);
        }
    }

    private async Task TrySendAsync(long chatId, string html, CancellationToken ct)
    {
        try
        {
            await notifier.SendToAsync(chatId, html, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The sign-in result could not be sent");
        }
    }

    // Runs even when the invocation is cancelled: a left-over attempt would block /login until it expires.
    private async Task TryDeleteAsync(string attemptId)
    {
        try
        {
            await attempts.DeleteAsync(attemptId, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The sign-in attempt could not be deleted, it expires on its own");
        }
    }
}
