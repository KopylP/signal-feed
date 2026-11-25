using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure;

// The tools hold the changes of one turn, so each question gets its own instance.
public sealed class SettingsToolsFactory(SettingsEditor editor, IEffectiveOptions effective)
{
    public SettingsTools Create(List<SettingChange> draft) => new(editor, effective, draft);
}
