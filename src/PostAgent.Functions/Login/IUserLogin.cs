namespace PostAgent.Login;

// One MTProto sign-in on a fresh session, kept on one connection from the first step to the last.
public interface IUserLogin : IAsyncDisposable
{
    // Asks Telegram to send the login code to the account with this phone number.
    Task<LoginStep> StartAsync(string phone, CancellationToken ct);

    Task<LoginStep> SubmitCodeAsync(string code, CancellationToken ct);

    Task<LoginStep> SubmitPasswordAsync(string password, CancellationToken ct);

    // The signed-in account, for the confirmation; null before the sign-in is done.
    string? AccountName { get; }

    // The new session; call it once, after Done. It ends the connection.
    Task<byte[]> ExportSessionAsync(CancellationToken ct);
}
