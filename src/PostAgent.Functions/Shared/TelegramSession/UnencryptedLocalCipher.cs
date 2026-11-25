namespace PostAgent.Shared.TelegramSession;

// Local development against Azurite, where there is no Key Vault. Registered only with a storage connection string,
// never in Azure (SessionCipherServices refuses to start there without SessionKeyId).
public sealed class UnencryptedLocalCipher : ISessionCipher
{
    public bool Encrypts => false;

    public Task<byte[]> ProtectAsync(byte[] plaintext, string purpose, CancellationToken ct) => Task.FromResult(plaintext);

    public byte[] Protect(byte[] plaintext, string purpose) => plaintext;

    public Task<byte[]> UnprotectAsync(byte[] data, string purpose, CancellationToken ct) => Task.FromResult(data);
}
