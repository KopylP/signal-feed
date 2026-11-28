namespace PostAgent.Shared.Settings;

// One step of a proposal. Target is a setting key, a group name, a source name or a prompt section id.
// Value is used by Set and by the text edits, where Before is the fragment that Value replaces (empty: add at the end).
// RewritePrompt carries the whole new text of a section in Value.
// Structured on purpose: the confirmation text is built from these fields by code, never taken from the model's words.
public sealed record SettingChange(SettingChangeKind Kind, string Target, string? Value = null, string? Before = null);
