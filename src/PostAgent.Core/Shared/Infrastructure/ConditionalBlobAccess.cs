using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace PostAgent.Shared.Infrastructure;

// Optimistic concurrency over one blob: read with its ETag, then write or delete only that version.
// Every store that keeps a small document in Blob Storage (settings, reader memory, votes, usage, conversation, sign-in) uses these
// instead of repeating the request conditions and the status codes of a lost race.
public static class ConditionalBlobAccess
{
    // Null when there is no blob.
    public static async Task<BlobContent?> TryDownloadAsync(this BlobClient blob, CancellationToken ct)
    {
        try
        {
            var result = (await blob.DownloadContentAsync(ct)).Value;
            return new BlobContent(result.Content, result.Details.ETag, result.Details.LastModified);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // Writes over the version that was read (ifMatch), or, with null, only when there is no blob yet.
    // False when someone else wrote or deleted it first: read again and retry, or give up.
    public static async Task<bool> TryUploadAsync(this BlobClient blob, BinaryData data, ETag? ifMatch, CancellationToken ct)
    {
        var conditions = ifMatch is { } match
            ? new BlobRequestConditions { IfMatch = match }
            : new BlobRequestConditions { IfNoneMatch = ETag.All };
        try
        {
            await blob.UploadAsync(data, new BlobUploadOptions { Conditions = conditions }, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            return false;
        }
    }

    // True when this call deleted the version that was read; false when it was gone or changed meanwhile.
    public static async Task<bool> TryDeleteAsync(this BlobClient blob, ETag ifMatch, CancellationToken ct)
    {
        try
        {
            return (await blob.DeleteIfExistsAsync(conditions: new BlobRequestConditions { IfMatch = ifMatch }, cancellationToken: ct)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            return false;
        }
    }
}
