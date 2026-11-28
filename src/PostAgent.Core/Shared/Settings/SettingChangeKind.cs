namespace PostAgent.Shared.Settings;

// Stored as a number in the waiting proposals of overrides.json, so a value never moves: a removed kind leaves a gap.
public enum SettingChangeKind
{
    Set,
    Reset,
    ResetGroup,
    AddSource,
    RemoveSource,
    ToggleAlertSource,
    AddWatch,
    RemoveWatch,
    EditPrompt,
    ResetPrompt,

    // 10 was the reader profile edit (ADR-0015), replaced by the reader memory (ADR-0022); a stored step with it is refused as unknown.
    ClearContext = 11,
    RewritePrompt = 12,
}
