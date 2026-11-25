namespace PostAgent.Shared.Settings;

// What the reader wrote about themselves, one plain text blob, separate from the settings. The chat reads it for every question.
// Writers use the ETag, like the overrides.
public interface IProfileStore
{
    // Null when nothing was saved yet.
    Task<ProfileHandle?> LoadAsync(CancellationToken ct);

    // current null: create only if the blob does not exist. False when the ETag no longer matches.
    Task<bool> TrySaveAsync(ProfileHandle? current, string text, CancellationToken ct);
}
