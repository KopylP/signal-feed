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
            if (await SignInAsync(login, attempt, ct) is { } result)
            {
                await notifier.SendToAsync(attempt.ChatId, result, ct);
            }
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

    // Returns the message that ends a sign-in that did not replace the session (a replaced one is announced to every chat).
    private async Task<string?> SignInAsync(IUserLogin login, LoginAttempt attempt, CancellationToken ct)
    {
        if (LoginButtons.Build(options.Value.WebsiteHostName, attempt.Id) is not { } buttons)
        {
            return LoginTexts.NoHost;
        }

        await panel.SendAsync(attempt.ChatId, LoginTexts.AskPhone((int)LoginInbox.AttemptLifetime.TotalMinutes), buttons.Phone, ct);

        var progress = new LoginProgress();
        while (!attempt.IsExpired(time.GetUtcNow()))
        {
            var submission = await attempts.TakeSubmissionAsync(attempt.Id, ct);
            if (submission is null)
            {
                await Task.Delay(PollInterval, time, ct);
                continue;
            }

            if (await AdvanceAsync(login, attempt, buttons, progress, submission, ct) is { } end)
            {
                return end;
            }

            if (progress.Step == LoginStep.Done)
            {
                return await SaveAsync(login, progress.MaskedPhone, ct);
            }

            if (progress.Step == LoginStep.Unsupported)
            {
                return LoginTexts.Unsupported;
            }
        }

        log.LogInformation("Sign-in expired before it was finished");
        return LoginTexts.Expired;
    }

    // One submission moves the sign-in on. Each stage takes only its own field: a code sent before the phone, or a proof before the challenge, is ignored.
    // Returns the message that ends a failed sign-in (too many tries, an unsupported account), or null while it goes on; progress.Step says where.
    private async Task<string?> AdvanceAsync(
        IUserLogin login, LoginAttempt attempt, LoginButtons buttons, LoginProgress progress, LoginSubmission submission, CancellationToken ct) =>
        progress.Step switch
        {
            LoginStep.PhoneRequired when submission.Phone is { } phone => await SubmitPhoneAsync(login, attempt, buttons, progress, phone, ct),
            LoginStep.CodeRequired when submission.Code is { } code => await SubmitCodeAsync(login, attempt, buttons, progress, code, ct),
            LoginStep.PasswordRequired when submission.Proof is { } proof => await SubmitPasswordAsync(login, attempt, buttons, progress, proof, ct),
            _ => null,
        };

    private async Task<string?> SubmitPhoneAsync(
        IUserLogin login, LoginAttempt attempt, LoginButtons buttons, LoginProgress progress, string phone, CancellationToken ct)
    {
        progress.Step = await login.StartAsync(phone, ct);
        progress.MaskedPhone = LoginInput.MaskPhone(phone);
        if (progress.Step == LoginStep.CodeRequired)
        {
            log.LogInformation("Sign-in code requested");
            await panel.SendAsync(attempt.ChatId, LoginTexts.CodeSent(progress.MaskedPhone), buttons.Code, ct);
        }

        return null;
    }

    private async Task<string?> SubmitCodeAsync(
        IUserLogin login, LoginAttempt attempt, LoginButtons buttons, LoginProgress progress, string code, CancellationToken ct)
    {
        progress.Step = await login.SubmitCodeAsync(code, ct);
        if (progress.Step == LoginStep.CodeInvalid)
        {
            if (progress.CountFailure())
            {
                return LoginTexts.TooManyTries;
            }

            await panel.SendAsync(attempt.ChatId, LoginTexts.WrongCode, buttons.Code, ct);
            progress.Step = LoginStep.CodeRequired;
            return null;
        }

        return progress.Step == LoginStep.PasswordRequired && !await AskPasswordAsync(login, attempt, LoginTexts.PasswordNeeded, buttons.Password, ct)
            ? LoginTexts.Unsupported
            : null;
    }

    private async Task<string?> SubmitPasswordAsync(
        IUserLogin login, LoginAttempt attempt, LoginButtons buttons, LoginProgress progress, PasswordProof proof, CancellationToken ct)
    {
        progress.Step = await login.SubmitPasswordProofAsync(proof, ct);
        if (progress.Step != LoginStep.PasswordInvalid)
        {
            return null;
        }

        if (progress.CountFailure())
        {
            return LoginTexts.TooManyTries;
        }

        progress.Step = LoginStep.PasswordRequired;
        return await AskPasswordAsync(login, attempt, LoginTexts.WrongPassword, buttons.Password, ct) ? null : LoginTexts.Unsupported;
    }

    // Every proof is good for one challenge, so each try gets a fresh one; the page reads it through LoginForm.
    private async Task<bool> AskPasswordAsync(IUserLogin login, LoginAttempt attempt, string text, InlineKeyboard button, CancellationToken ct)
    {
        if (await login.GetPasswordChallengeAsync(ct) is not { } challenge)
        {
            return false;
        }

        await attempts.SetChallengeAsync(attempt.Id, challenge, ct);
        await panel.SendAsync(attempt.ChatId, text, button, ct);
        return true;
    }

    // Any paired chat may sign in any account, so every paired chat hears which account reads the channels now:
    // a swap of the reader account never goes unnoticed. The chat that signed in gets the same message.
    private async Task<string?> SaveAsync(IUserLogin login, string maskedPhone, CancellationToken ct)
    {
        var account = login.AccountName;
        await sessions.SaveAsync(await login.ExportSessionAsync(ct), ct);
        log.LogInformation("Sign-in finished, the Telegram session was replaced");
        var text = LoginTexts.Replaced(account, maskedPhone);
        try
        {
            await notifier.SendAsync(text, ct);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The session is saved: the sign-in succeeded even if the announcement did not reach every chat.
            log.LogWarning(ex, "The new session could not be announced to every chat");
            return text;
        }
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
