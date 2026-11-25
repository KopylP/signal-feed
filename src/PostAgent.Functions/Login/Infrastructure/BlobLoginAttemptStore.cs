using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Settings;

namespace PostAgent.Login.Infrastructure;

// One blob next to the session, so that only the identities that may read the session can read a code or a password in transit.
// Every change is a read, a check and a write with the ETag; a lost race is simply retried.
public sealed class BlobLoginAttemptStore(BlobServiceClient blobs, IOptions<AgentOptions> options) : ILoginAttemptStore
{
    private const int MaxRetries = 3;
    private static readonly LoginAttempt Unreadable = new(string.Empty, 0, DateTimeOffset.MinValue);

    private BlobClient Blob => blobs.GetBlobContainerClient(options.Value.SessionBlobContainer).GetBlobClient(StorageNames.LoginAttemptBlob);

    public async Task<LoginAttempt?> GetAsync(CancellationToken ct) => (await ReadAsync(ct))?.Attempt;

    public async Task<bool> TryStartAsync(LoginAttempt attempt, DateTimeOffset now, CancellationToken ct)
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            var current = await ReadAsync(ct);
            if (current is { Attempt: var existing } && !existing.IsExpired(now))
            {
                return false;
            }

            if (await TryWriteAsync(attempt, current?.ETag, ct))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> TrySubmitAsync(string attemptId, LoginSubmission submission, DateTimeOffset now, CancellationToken ct)
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            if (await ReadAsync(ct) is not { Attempt: var attempt, ETag: var etag }
                || attempt.Id != attemptId
                || attempt.IsExpired(now)
                || attempt.Submissions >= LoginAttempt.MaxSubmissions)
            {
                return false;
            }

            var updated = attempt with { Phone = submission.Phone, Code = submission.Code, Password = submission.Password, Submissions = attempt.Submissions + 1 };
            if (await TryWriteAsync(updated, etag, ct))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<LoginSubmission?> TakeSubmissionAsync(string attemptId, CancellationToken ct)
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            if (await ReadAsync(ct) is not { Attempt: var attempt, ETag: var etag } || attempt.Id != attemptId)
            {
                return null;
            }

            if (attempt.Phone is null && attempt.Code is null && attempt.Password is null)
            {
                return null;
            }

            if (await TryWriteAsync(attempt with { Phone = null, Code = null, Password = null }, etag, ct))
            {
                return new LoginSubmission(attempt.Phone, attempt.Code, attempt.Password);
            }
        }

        return null;
    }

    public async Task DeleteAsync(string attemptId, CancellationToken ct)
    {
        if (await ReadAsync(ct) is not { Attempt: var attempt, ETag: var etag } || attempt.Id != attemptId)
        {
            return;
        }

        try
        {
            await Blob.DeleteIfExistsAsync(conditions: new BlobRequestConditions { IfMatch = etag }, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Changed meanwhile (a late form): try once more on the new version.
            if (await ReadAsync(ct) is { Attempt.Id: var id, ETag: var newer } && id == attemptId)
            {
                await Blob.DeleteIfExistsAsync(conditions: new BlobRequestConditions { IfMatch = newer }, cancellationToken: ct);
            }
        }
    }

    private async Task<(LoginAttempt Attempt, ETag ETag)?> ReadAsync(CancellationToken ct)
    {
        BlobDownloadResult content;
        try
        {
            content = (await Blob.DownloadContentAsync(ct)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        try
        {
            var attempt = JsonSerializer.Deserialize<LoginAttempt>(content.Content.ToMemory().Span);
            if (attempt is not null)
            {
                return (attempt, content.Details.ETag);
            }
        }
        catch (JsonException)
        {
        }

        // A blob that cannot be read is treated as an expired attempt, so the next start replaces it instead of being blocked by it.
        return (Unreadable, content.Details.ETag);
    }

    private async Task<bool> TryWriteAsync(LoginAttempt attempt, ETag? etag, CancellationToken ct)
    {
        var conditions = etag is { } match
            ? new BlobRequestConditions { IfMatch = match }
            : new BlobRequestConditions { IfNoneMatch = ETag.All };
        try
        {
            await Blob.UploadAsync(BinaryData.FromObjectAsJson(attempt), new BlobUploadOptions { Conditions = conditions }, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            return false;
        }
    }
}
