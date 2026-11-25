namespace PostAgent.Shared.Settings;

// Unreadable: the blob exists but cannot be parsed. Value is then empty, and only an explicit reset may replace the blob.
public sealed record OverridesHandle(SettingsOverrides Value, string ETag, bool Unreadable = false);
