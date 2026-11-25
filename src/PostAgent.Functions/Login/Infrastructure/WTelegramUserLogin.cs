using System.Globalization;
using PostAgent.Shared.Settings;
using TL;
using WTelegram;

namespace PostAgent.Login.Infrastructure;

// A sign-in on a new, empty session (a new auth key): the session the collector holds is never touched until this one is complete.
// It drives auth.sendCode, auth.signIn and auth.checkPassword itself (through Invoke: WTelegram marks its helpers for these
// obsolete to steer callers to its Login dialogue) instead of that dialogue, for two reasons:
// the 2FA step takes an SRP proof computed on the owner's phone (the password never reaches us), and an abandoned sign-in
// can be cancelled without leaving a thread blocked inside the library.
public sealed class WTelegramUserLogin : IUserLogin
{
    private readonly AgentOptions _options;
    private readonly MemoryStream _session = new();
    private readonly Client _client;
    private string? _phone;
    private string? _phoneCodeHash;
    private long? _srpId;
    private bool _done;
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
        await _client.ConnectAsync();
        _phone = phone;

        Auth_SentCodeBase sent;
        try
        {
            sent = await SendCodeAsync(phone);
        }
        catch (RpcException ex) when (ex.Message == "AUTH_RESTART")
        {
            sent = await SendCodeAsync(phone);
        }

        // A Firebase SMS needs a device token we do not have: ask Telegram for the next way of sending the code.
        if (sent is Auth_SentCode { type: Auth_SentCodeTypeFirebaseSms } firebase)
        {
            sent = await _client.Invoke(new TL.Methods.Auth_ResendCode { phone_number = phone, phone_code_hash = firebase.phone_code_hash });
        }

        switch (sent)
        {
            case Auth_SentCodeSuccess success:
                return Finish(success.authorization);
            case Auth_SentCode { type: Auth_SentCodeTypeSetUpEmailRequired or Auth_SentCodeTypeFirebaseSms }:
                return LoginStep.Unsupported;
            case Auth_SentCode code:
                _phoneCodeHash = code.phone_code_hash;
                return LoginStep.CodeRequired;
            default:
                return LoginStep.Unsupported;
        }
    }

    public async Task<LoginStep> SubmitCodeAsync(string code, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_phone is null || _phoneCodeHash is null)
        {
            throw new InvalidOperationException("The code was submitted before Telegram sent one.");
        }

        try
        {
            return Finish(await _client.Invoke(new TL.Methods.Auth_SignIn
            {
                flags = TL.Methods.Auth_SignIn.Flags.has_phone_code,
                phone_number = _phone,
                phone_code_hash = _phoneCodeHash,
                phone_code = code,
            }));
        }
        catch (RpcException ex) when (ex.Message is "PHONE_CODE_INVALID" or "PHONE_CODE_EMPTY")
        {
            return LoginStep.CodeInvalid;
        }
        catch (RpcException ex) when (ex.Message == "SESSION_PASSWORD_NEEDED")
        {
            return LoginStep.PasswordRequired;
        }
    }

    public async Task<PasswordChallenge?> GetPasswordChallengeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var password = await _client.Account_GetPassword();
        if (password.current_algo is not PasswordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow algo
            || algo.p is not { Length: 256 } || algo.g is < 2 or > 7 || password.srp_B is not { Length: > 0 })
        {
            return null;
        }

        _srpId = password.srp_id;
        return new PasswordChallenge(password.srp_id, algo.g, algo.p, algo.salt1, algo.salt2, password.srp_B);
    }

    public async Task<LoginStep> SubmitPasswordProofAsync(PasswordProof proof, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_srpId is not { } srpId)
        {
            throw new InvalidOperationException("A password proof was submitted before a challenge was issued.");
        }

        try
        {
            return Finish(await _client.Auth_CheckPassword(new InputCheckPasswordSRP { srp_id = srpId, A = proof.A, M1 = proof.M1 }));
        }
        catch (RpcException ex) when (ex.Message is "PASSWORD_HASH_INVALID" or "SRP_ID_INVALID" or "SRP_PASSWORD_CHANGED")
        {
            // Every proof is tied to one challenge: a new one is needed for the next try.
            _srpId = null;
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

    public async ValueTask DisposeAsync()
    {
        // An unfinished sign-in: tell Telegram the code is no longer valid.
        if (!_done && !_disposed && _phone is not null && _phoneCodeHash is not null)
        {
            try
            {
                await _client.Invoke(new TL.Methods.Auth_CancelCode { phone_number = _phone, phone_code_hash = _phoneCodeHash });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best effort: the code expires on its own.
            }
        }

        Close();
    }

    private Task<Auth_SentCodeBase> SendCodeAsync(string phone) =>
        _client.Invoke(new TL.Methods.Auth_SendCode
        {
            phone_number = phone,
            api_id = _options.TelegramApiId,
            api_hash = _options.TelegramApiHash,
            settings = new CodeSettings(),
        });

    private LoginStep Finish(Auth_AuthorizationBase authorization)
    {
        if (authorization is not Auth_Authorization)
        {
            return LoginStep.Unsupported; // sign-up required: this number has no Telegram account
        }

        _client.LoginAlreadyDone(authorization);
        _done = true;
        return LoginStep.Done;
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

    // Only the app credentials come from the callback; the phone, code and proof go through the calls above.
    private string? Config(string what) => what switch
    {
        "api_id" => _options.TelegramApiId.ToString(CultureInfo.InvariantCulture),
        "api_hash" => _options.TelegramApiHash,
        _ => null,
    };
}
