namespace PostAgent.Login;

// One sign-in from the bot (ADR-0016). Only one exists at a time; it lives in the session container next to the session.
// Phone, Code and Password hold what the Mini App submitted until the waiting function takes them, never longer.
public sealed record LoginAttempt(
    string Id,
    long ChatId,
    DateTimeOffset ExpiresAt,
    string? Phone = null,
    string? Code = null,
    string? Password = null,
    int Submissions = 0)
{
    // A form can be sent a few times (the phone, a wrong code, the password), but not used to guess codes.
    public const int MaxSubmissions = 6;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
}
