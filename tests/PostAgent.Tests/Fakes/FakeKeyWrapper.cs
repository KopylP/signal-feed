using System.Security.Cryptography;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Tests.Fakes;

// Stands in for Key Vault: wraps with a local AES key and counts the calls, so tests can see when Key Vault would be asked.
internal sealed class FakeKeyWrapper : IKeyWrapper
{
    public const string RotatedKeyId = "https://kv-test.vault.azure.net/keys/tg-session/v2";

    // The current key version: what a new wrap is made with (Key Vault wraps with the latest version of the versionless key).
    public string KeyId { get; set; } = "https://kv-test.vault.azure.net/keys/tg-session/v1";

    private readonly byte[] _kek = RandomNumberGenerator.GetBytes(32);

    public int Wraps { get; private set; }
    public int Unwraps { get; private set; }

    public Task<WrappedKey> WrapAsync(byte[] dataKey, CancellationToken ct) => Task.FromResult(Wrap(dataKey));

    public WrappedKey Wrap(byte[] dataKey)
    {
        Wraps++;
        using var aes = Aes.Create();
        aes.Key = _kek;
        return new WrappedKey(KeyId, aes.EncryptEcb(dataKey, PaddingMode.None));
    }

    public Task<byte[]> UnwrapAsync(WrappedKey wrapped, CancellationToken ct)
    {
        Unwraps++;
        using var aes = Aes.Create();
        aes.Key = _kek;
        return Task.FromResult(aes.DecryptEcb(wrapped.Key, PaddingMode.None));
    }
}
