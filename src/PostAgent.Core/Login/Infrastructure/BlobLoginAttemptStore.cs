using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Settings;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Login.Infrastructure;

// One blob next to the session, encrypted with the Key Vault key (ADR-0017): storage access alone does not reveal a phone or a code in transit.
// It never holds the 2FA password: only the SRP proof computed on the phone, which is single-use.
// Every change is a read, a check and a write with the ETag; a lost race is simply retried.
public sealed class BlobLoginAttemptStore(BlobServiceClient blobs, ISessionCipher cipher, IOptions<AgentOptions> options) : ILoginAttemptStore
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

            var updated = attempt with { Phone = submission.Phone, Code = submission.Code, Proof = submission.Proof, Submissions = attempt.Submissions + 1 };
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

            if (attempt.Phone is null && attempt.Code is null && attempt.Proof is null)
            {
                return null;
            }

            if (await TryWriteAsync(attempt with { Phone = null, Code = null, Proof = null }, etag, ct))
            {
                return new LoginSubmission(attempt.Phone, attempt.Code, attempt.Proof);
            }
        }

        return null;
    }

    public async Task SetChallengeAsync(string attemptId, PasswordChallenge challenge, CancellationToken ct)
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            if (await ReadAsync(ct) is not { Attempt: var attempt, ETag: var etag } || attempt.Id != attemptId)
            {
                return;
            }

            if (await TryWriteAsync(attempt with { Challenge = challenge, Proof = null }, etag, ct))
            {
                return;
            }
        }

        throw new InvalidOperationException("The password challenge could not be published.");
    }

    public async Task DeleteAsync(string attemptId, CancellationToken ct)
    {
        // A late form may change it meanwhile: then once more on the new version.
        for (var i = 0; i < 2; i++)
        {
            if (await ReadAsync(ct) is not { Attempt: var attempt, ETag: var etag } || attempt.Id != attemptId || await Blob.TryDeleteAsync(etag, ct))
            {
                return;
            }
        }
    }

    private async Task<(LoginAttempt Attempt, ETag ETag)?> ReadAsync(CancellationToken ct)
    {
        if (await Blob.TryDownloadAsync(ct) is not { } content)
        {
            return null;
        }

        try
        {
            var json = await cipher.UnprotectAsync(content.Data.ToArray(), StorageNames.LoginAttemptBlob, ct);
            var attempt = JsonSerializer.Deserialize<LoginAttempt>(json);
            if (attempt is not null)
            {
                return (attempt, content.ETag);
            }
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException)
        {
            // Not ours to read (not encrypted, or with another key): treated like any unreadable attempt below.
        }

        // A blob that cannot be read is treated as an expired attempt, so the next start replaces it instead of being blocked by it.
        return (Unreadable, content.ETag);
    }

    private async Task<bool> TryWriteAsync(LoginAttempt attempt, ETag? etag, CancellationToken ct)
    {
        var sealedAttempt = await cipher.ProtectAsync(JsonSerializer.SerializeToUtf8Bytes(attempt), StorageNames.LoginAttemptBlob, ct);
        return await Blob.TryUploadAsync(BinaryData.FromBytes(sealedAttempt), etag, ct);
    }
}
