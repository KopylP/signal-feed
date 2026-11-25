namespace PostAgent.Shared.Settings;

// The one blob with what the owner set from the bot. Writers use the ETag, so two taps or two instances cannot overwrite each other.
public interface IOverridesStore
{
    // Null when the blob is missing or cannot be read (the second case is logged).
    Task<OverridesHandle?> LoadAsync(CancellationToken ct);

    // current null: create only if the blob does not exist. False when the ETag no longer matches.
    Task<bool> TrySaveAsync(OverridesHandle? current, SettingsOverrides updated, CancellationToken ct);
}
