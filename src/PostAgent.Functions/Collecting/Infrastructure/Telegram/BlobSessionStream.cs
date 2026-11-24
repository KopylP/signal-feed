using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Collecting.Infrastructure.Telegram;

// Сховище сесії WTelegramClient у Blob Storage. Читається в пам'ять, а кожен Flush
// (бібліотека викликає його при зміні сесії) завантажує вміст назад у blob.
public sealed class BlobSessionStream : MemoryStream
{
    private readonly BlobClient _blob;

    private BlobSessionStream(BlobClient blob, byte[] content)
    {
        _blob = blob;
        Write(content, 0, content.Length);
        Position = 0;
    }

    public static async Task<BlobSessionStream> LoadAsync(BlobContainerClient container, CancellationToken ct)
    {
        var blob = container.GetBlobClient(StorageNames.SessionBlob);
        if (!await blob.ExistsAsync(ct))
        {
            throw new InvalidOperationException("Telegram session not found in blob storage. Run TgLogin first.");
        }

        var content = (await blob.DownloadContentAsync(ct)).Value.Content.ToArray();
        return new BlobSessionStream(blob, content);
    }

    public override void Flush()
    {
        base.Flush();
        _blob.Upload(BinaryData.FromBytes(ToArray()), overwrite: true);
    }
}
