namespace PostAgent.Shared.Settings;

// Min and Max are 0 unless the kind is Int; Choices is empty unless the kind is Choice.
public sealed record SettingDefinition(
    string Key,
    SettingGroup Group,
    SettingKind Kind,
    string Label,
    int Min,
    int Max,
    IReadOnlyList<SettingChoice> Choices);
