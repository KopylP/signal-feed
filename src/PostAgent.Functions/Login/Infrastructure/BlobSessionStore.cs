using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Settings;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Login.Infrastructure;

// The same blob TgLogin writes and the collector reads, encrypted with the Key Vault key (ADR-0017).
public sealed class BlobSessionStore(BlobServiceClient blobs, ISessionCipher cipher, IOptions<AgentOptions> options) : ISessionStore
{
    public async Task SaveAsync(byte[] session, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(options.Value.SessionBlobContainer);
        var sealedSession = await cipher.ProtectAsync(session, StorageNames.SessionBlob, ct);
        await container.GetBlobClient(StorageNames.SessionBlob).UploadAsync(BinaryData.FromBytes(sealedSession), overwrite: true, ct);
    }
}
