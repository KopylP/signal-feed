namespace PostAgent.Shared.Settings;

// One step of a proposal. Target is a setting key, a group name or a source name; Value is only used by Set.
// Structured on purpose: the confirmation text is built from these fields by code, never taken from the model's words.
public sealed record SettingChange(SettingChangeKind Kind, string Target, string? Value = null);
