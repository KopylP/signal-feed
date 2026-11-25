using System.Collections.Concurrent;

namespace PostAgent.Shared.TelegramSession;

// Envelope encryption with one data key per process: it is wrapped by Key Vault once, with the current key version, and every blob
// gets a fresh nonce. A data key read from a blob is unwrapped once and cached for reading only: writing always uses the process's
// own key, so after a rotation every rewritten blob moves to the new key version (the session is rewritten on every collection).
public sealed class EnvelopeCipher(IKeyWrapper wrapper) : ISessionCipher
{
    private const int MaxCachedKeys = 16;

    private readonly ConcurrentDictionary<string, byte[]> _unwrapped = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile SessionDataKey? _current;

    public bool Encrypts => true;

    public async Task<byte[]> ProtectAsync(byte[] plaintext, string purpose, CancellationToken ct)
    {
        var current = _current ?? await CreateCurrentAsync(ct);
        return SessionEnvelope.Seal(current.Key, current.Wrapped, plaintext, purpose);
    }

    public byte[] Protect(byte[] plaintext, string purpose)
    {
        var current = _current ?? CreateCurrent();
        return SessionEnvelope.Seal(current.Key, current.Wrapped, plaintext, purpose);
    }

    public async Task<byte[]> UnprotectAsync(byte[] data, string purpose, CancellationToken ct)
    {
        var wrapped = SessionEnvelope.ReadKey(data);
        var cacheKey = wrapped.KeyId + "|" + Convert.ToBase64String(wrapped.Key);
        if (!_unwrapped.TryGetValue(cacheKey, out var dataKey))
        {
            dataKey = await wrapper.UnwrapAsync(wrapped, ct);
            if (_unwrapped.Count >= MaxCachedKeys)
            {
                _unwrapped.Clear();
            }

            _unwrapped[cacheKey] = dataKey;
        }

        return SessionEnvelope.Open(dataKey, data, purpose);
    }

    private async Task<SessionDataKey> CreateCurrentAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_current is not { } current)
            {
                var dataKey = SessionEnvelope.NewDataKey();
                current = new SessionDataKey(dataKey, await wrapper.WrapAsync(dataKey, ct));
                _current = current;
            }

            return current;
        }
        finally
        {
            _gate.Release();
        }
    }

    private SessionDataKey CreateCurrent()
    {
        _gate.Wait();
        try
        {
            if (_current is not { } current)
            {
                var dataKey = SessionEnvelope.NewDataKey();
                current = new SessionDataKey(dataKey, wrapper.Wrap(dataKey));
                _current = current;
            }

            return current;
        }
        finally
        {
            _gate.Release();
        }
    }
}
