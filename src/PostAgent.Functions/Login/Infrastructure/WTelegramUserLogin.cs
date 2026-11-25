using System.Globalization;
using PostAgent.Shared.Settings;
using TL;
using WTelegram;

namespace PostAgent.Login.Infrastructure;

// A sign-in on a new, empty session (a new auth key): the session the collector holds is never touched until this one is complete.
// WTelegram's Login(string) is a dialogue: each call answers the last question and returns the next one, null when signed in.
// A wrong code or password returns the same question again; after its third wrong answer WTelegram throws and the sign-in is over.
public sealed class WTelegramUserLogin : IUserLogin
{
    private readonly AgentOptions _options;
    private readonly MemoryStream _session = new();
    private readonly Client _client;
    private bool _disposed;

    public WTelegramUserLogin(AgentOptions options)
    {
        _options = options;
        _client = new Client(Config, _session);
    }

    public string? AccountName => _client.User is { } user ? user.MainUsername ?? $"{user.first_name} {user.last_name}".Trim() : null;

    public async Task<LoginStep> StartAsync(string phone, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Map(await _client.Login(phone));
    }

    public async Task<LoginStep> SubmitCodeAsync(string code, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var next = await _client.Login(code);
            return next == "verification_code" ? LoginStep.CodeInvalid : Map(next);
        }
        catch (RpcException ex) when (ex.Message is "PHONE_CODE_INVALID" or "PHONE_CODE_EMPTY")
        {
            return LoginStep.CodeInvalid;
        }
    }

    public async Task<LoginStep> SubmitPasswordAsync(string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var next = await _client.Login(password);
            return next == "password" ? LoginStep.PasswordInvalid : Map(next);
        }
        catch (RpcException ex) when (ex.Message == "PASSWORD_HASH_INVALID")
        {
            return LoginStep.PasswordInvalid;
        }
    }

    public Task<byte[]> ExportSessionAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Disposing the client writes the final session into the stream; ToArray still works on a disposed MemoryStream.
        Close();
        return Task.FromResult(_session.ToArray());
    }

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }

    private void Close()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
        _session.Dispose();
    }

    private static LoginStep Map(string? next) => next switch
    {
        null => LoginStep.Done,
        "verification_code" => LoginStep.CodeRequired,
        "password" => LoginStep.PasswordRequired,
        _ => LoginStep.Unsupported,
    };

    // Only the app credentials come from the callback; the phone, code and password go through Login(string).
    private string? Config(string what) => what switch
    {
        "api_id" => _options.TelegramApiId.ToString(CultureInfo.InvariantCulture),
        "api_hash" => _options.TelegramApiHash,
        _ => null,
    };
}
