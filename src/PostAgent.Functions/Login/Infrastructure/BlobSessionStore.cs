using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Settings;

namespace PostAgent.Login.Infrastructure;

// The same blob TgLogin writes and the collector reads.
public sealed class BlobSessionStore(BlobServiceClient blobs, IOptions<AgentOptions> options) : ISessionStore
{
    public async Task SaveAsync(byte[] session, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(options.Value.SessionBlobContainer);
        await container.GetBlobClient(StorageNames.SessionBlob).UploadAsync(BinaryData.FromBytes(session), overwrite: true, ct);
    }
}
