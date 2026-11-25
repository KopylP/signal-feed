using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Collecting.Infrastructure.Telegram;

// The WTelegramClient session in Blob Storage, encrypted with the Key Vault key (ADR-0017). It is read into memory,
// and every Flush (the library calls it when the session changes) encrypts it and uploads it back.
public sealed class BlobSessionStream : MemoryStream
{
    private readonly BlobClient _blob;
    private readonly ISessionCipher _cipher;

    private BlobSessionStream(BlobClient blob, ISessionCipher cipher, byte[] content)
    {
        _blob = blob;
        _cipher = cipher;
        Write(content, 0, content.Length);
        Position = 0;
    }

    // True when the blob was still in clear (written before encryption): LoadAsync has just stored it encrypted.
    public bool WasUnencrypted { get; private init; }

    public static async Task<BlobSessionStream> LoadAsync(BlobContainerClient container, ISessionCipher cipher, CancellationToken ct)
    {
        var blob = container.GetBlobClient(StorageNames.SessionBlob);
        if (!await blob.ExistsAsync(ct))
        {
            throw new InvalidOperationException("Telegram session not found in blob storage. Send /login to the bot (or run TgLogin).");
        }

        var stored = (await blob.DownloadContentAsync(ct)).Value.Content.ToArray();
        if (!cipher.Encrypts || SessionEnvelope.IsSealed(stored))
        {
            var session = await cipher.UnprotectAsync(stored, StorageNames.SessionBlob, ct);

            // Wrap this process's data key now: Flush is synchronous and should not wait for Key Vault.
            await cipher.ProtectAsync([], StorageNames.SessionBlob, ct);
            return new BlobSessionStream(blob, cipher, session);
        }

        // A session from before encryption: encrypt it right away rather than wait for the library to flush.
        await blob.UploadAsync(BinaryData.FromBytes(await cipher.ProtectAsync(stored, StorageNames.SessionBlob, ct)), overwrite: true, ct);
        return new BlobSessionStream(blob, cipher, stored) { WasUnencrypted = true };
    }

    public override void Flush()
    {
        base.Flush();
        _blob.Upload(BinaryData.FromBytes(_cipher.Protect(ToArray(), StorageNames.SessionBlob)), overwrite: true);
    }
}
