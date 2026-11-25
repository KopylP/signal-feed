using System.Globalization;
using PostAgent.Shared.Settings;

namespace PostAgent.SettingsMenu;

// The text under a button, at most 64 bytes. The screen is in the data, so navigation needs no stored state, and a number button
// carries the target value ("n:DigestSize:8"), so a double press sets 8 twice instead of stepping twice.
public static class CallbackData
{
    public static string Encode(MenuAction a) => a.Kind switch
    {
        MenuActionKind.Main => "m",
        MenuActionKind.OpenGroup => $"g:{a.Arg}",
        MenuActionKind.OpenSources => "s",
        MenuActionKind.OpenChoice => $"o:{a.Arg}",
        MenuActionKind.SetInt => $"n:{a.Arg}:{a.Number.ToString(CultureInfo.InvariantCulture)}",
        MenuActionKind.Choose => $"c:{a.Arg}:{a.Number.ToString(CultureInfo.InvariantCulture)}",
        MenuActionKind.AskText => $"t:{a.Arg}",
        MenuActionKind.Reset => $"r:{a.Arg}",
        MenuActionKind.ResetGroup => $"rg:{a.Arg}",
        MenuActionKind.Cancel => "x",
        MenuActionKind.ResetAll => "ra",
        MenuActionKind.AddSource => "sa",
        MenuActionKind.RemoveSource => MenuCallbacks.RemoveSource(a.Arg),
        MenuActionKind.ToggleAlert => $"sl:{a.Arg}",
        _ => throw new ArgumentOutOfRangeException(nameof(a)),
    };

    // Null for anything that is not data this bot produced; a button of an old message must never throw.
    public static MenuAction? Decode(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        var parts = data.Split(':');
        return (parts[0], parts.Length) switch
        {
            ("m", 1) => MenuAction.Main,
            ("s", 1) => MenuAction.OpenSources,
            ("x", 1) => MenuAction.Cancel,
            ("ra", 1) => MenuAction.ResetAll,
            ("sa", 1) => MenuAction.AddSource,
            ("g", 2) when Group(parts[1]) is { } group => MenuAction.OpenGroup(group),
            ("rg", 2) when Group(parts[1]) is { } group => MenuAction.ResetGroup(group),
            ("o", 2) when parts[1].Length > 0 => MenuAction.OpenChoice(parts[1]),
            ("t", 2) when parts[1].Length > 0 => MenuAction.AskText(parts[1]),
            ("r", 2) when parts[1].Length > 0 => MenuAction.Reset(parts[1]),
            ("sd", 2) when parts[1].Length > 0 => MenuAction.RemoveSource(parts[1]),
            ("sl", 2) when parts[1].Length > 0 => MenuAction.ToggleAlert(parts[1]),
            ("n", 3) when parts[1].Length > 0 && Number(parts[2]) is { } value => MenuAction.SetInt(parts[1], value),
            ("c", 3) when parts[1].Length > 0 && Number(parts[2]) is { } index => MenuAction.Choose(parts[1], index),
            _ => null,
        };
    }

    private static SettingGroup? Group(string name) =>
        Enum.TryParse<SettingGroup>(name, out var group) && Enum.IsDefined(group) ? group : null;

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
