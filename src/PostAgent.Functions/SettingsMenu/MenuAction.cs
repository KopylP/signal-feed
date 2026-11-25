using PostAgent.Shared.Settings;

namespace PostAgent.SettingsMenu;

// What a button press asks for. Arg is a setting key, a group name or a source name; Number is a target value or a choice index.
public sealed record MenuAction(MenuActionKind Kind, string Arg = "", int Number = 0)
{
    public static readonly MenuAction Main = new(MenuActionKind.Main);
    public static readonly MenuAction OpenSources = new(MenuActionKind.OpenSources);
    public static readonly MenuAction Cancel = new(MenuActionKind.Cancel);
    public static readonly MenuAction AddSource = new(MenuActionKind.AddSource);
    public static readonly MenuAction ResetAll = new(MenuActionKind.ResetAll);

    public static MenuAction OpenGroup(SettingGroup group) => new(MenuActionKind.OpenGroup, group.ToString());

    public static MenuAction OpenChoice(string key) => new(MenuActionKind.OpenChoice, key);

    public static MenuAction SetInt(string key, int value) => new(MenuActionKind.SetInt, key, value);

    public static MenuAction Choose(string key, int index) => new(MenuActionKind.Choose, key, index);

    public static MenuAction AskText(string key) => new(MenuActionKind.AskText, key);

    public static MenuAction Reset(string key) => new(MenuActionKind.Reset, key);

    public static MenuAction ResetGroup(SettingGroup group) => new(MenuActionKind.ResetGroup, group.ToString());

    public static MenuAction RemoveSource(string name) => new(MenuActionKind.RemoveSource, name);

    public static MenuAction ToggleAlert(string name) => new(MenuActionKind.ToggleAlert, name);
}
