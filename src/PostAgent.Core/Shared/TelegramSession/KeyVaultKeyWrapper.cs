using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace PostAgent.Shared.TelegramSession;

// RSA-OAEP-256 wrap and unwrap in Key Vault (ADR-0017). The private key never leaves the vault: whoever can read the blobs
// but holds no "Key Vault Crypto Service Encryption User" role on the key sees only ciphertext.
public sealed class KeyVaultKeyWrapper : IKeyWrapper
{
    private readonly TokenCredential _credential;
    private readonly CryptographyClient _current;
    private readonly string _keyPrefix;

    // keyId is the versionless id of the key (https://<vault>.vault.azure.net/keys/<name>); wrapping uses its current version.
    public KeyVaultKeyWrapper(string keyId, TokenCredential credential)
    {
        var uri = new Uri(keyId.TrimEnd('/'));
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Segments is not [_, "keys/", _])
        {
            throw new ArgumentException("SessionKeyId must be the versionless id of a Key Vault key: https://<vault>.vault.azure.net/keys/<name>.", nameof(keyId));
        }

        _credential = credential;
        _current = new CryptographyClient(uri, credential);
        _keyPrefix = uri.AbsoluteUri + "/";
    }

    public async Task<WrappedKey> WrapAsync(byte[] dataKey, CancellationToken ct)
    {
        var result = await _current.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, dataKey, ct);
        return new WrappedKey(result.KeyId, result.EncryptedKey);
    }

    public WrappedKey Wrap(byte[] dataKey)
    {
        var result = _current.WrapKey(KeyWrapAlgorithm.RsaOaep256, dataKey);
        return new WrappedKey(result.KeyId, result.EncryptedKey);
    }

    public async Task<byte[]> UnwrapAsync(WrappedKey wrapped, CancellationToken ct)
    {
        // The key id comes from the blob. Only a version of our own key is accepted: anything else would send
        // the app's Key Vault token to a vault named by whoever wrote the blob.
        if (!wrapped.KeyId.StartsWith(_keyPrefix, StringComparison.OrdinalIgnoreCase)
            || wrapped.KeyId.AsSpan(_keyPrefix.Length).IndexOfAny('/', '?', '#') >= 0)
        {
            throw new InvalidOperationException("The data key was wrapped by a key other than SessionKeyId.");
        }

        var client = new CryptographyClient(new Uri(wrapped.KeyId), _credential);
        return (await client.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrapped.Key, ct)).Key;
    }
}
