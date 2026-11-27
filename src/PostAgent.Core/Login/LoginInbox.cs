using System.Security.Cryptography;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;

namespace PostAgent.Login;

// /login from a bound private chat starts a sign-in (ADR-0016). The webhook must answer fast, so the sign-in itself runs from the queue.
public sealed class LoginInbox(
    ChatResolver resolver,
    ILoginAttemptStore attempts,
    ILoginQueue queue,
    ITelegramNotifier notifier,
    TimeProvider time,
    ILogger<LoginInbox> log) : IIncomingMessageHandler
{
    // Time to read the code, open the form and type it; also the longest the session lock is held.
    public static readonly TimeSpan AttemptLifetime = TimeSpan.FromMinutes(5);

    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { } message || !LoginCommand.Is(message.Text))
        {
            return false;
        }

        // Anyone else gets no answer: the bot is not an oracle for who may sign in.
        if (!await resolver.ServesAsync(message.Chat, ct))
        {
            log.LogInformation("Sign-in ignored: not a bound private chat");
            return true;
        }

        var chatId = message.Chat.Id;
        var attempt = new LoginAttempt(NewId(), chatId, time.GetUtcNow() + AttemptLifetime);
        if (!await attempts.TryStartAsync(attempt, time.GetUtcNow(), ct))
        {
            await notifier.SendToAsync(chatId, LoginTexts.InProgress, ct);
            return true;
        }

        try
        {
            await queue.EnqueueAsync(new LoginRequest(attempt.Id, chatId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Without a queued worker the attempt would only block the next /login until it expires.
            await attempts.DeleteAsync(attempt.Id, ct);
            throw;
        }

        log.LogInformation("Sign-in started");
        await notifier.SendToAsync(chatId, LoginTexts.Starting, ct);
        return true;
    }

    // 256 random bits: the id is part of what the Mini App must present, so it must not be guessable.
    private static string NewId() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
