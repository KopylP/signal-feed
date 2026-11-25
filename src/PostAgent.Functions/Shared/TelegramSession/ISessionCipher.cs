namespace PostAgent.Shared.TelegramSession;

// Encrypts what must not be readable from storage alone: the Telegram session and the sign-in in progress (ADR-0017).
// The purpose is the blob name; a blob opened under another name fails.
public interface ISessionCipher
{
    // False only for local development without Key Vault.
    bool Encrypts { get; }

    Task<byte[]> ProtectAsync(byte[] plaintext, string purpose, CancellationToken ct);

    // For the session stream, whose Flush is synchronous. Call ProtectAsync or UnprotectAsync once before, so no Key Vault call is needed here.
    byte[] Protect(byte[] plaintext, string purpose);

    Task<byte[]> UnprotectAsync(byte[] data, string purpose, CancellationToken ct);
}
