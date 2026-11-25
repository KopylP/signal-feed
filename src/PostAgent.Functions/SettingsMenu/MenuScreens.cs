using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.SettingsMenu;

// What the menu looks like: pure functions from the current settings to a message and its buttons.
public static class MenuScreens
{
    private static readonly (SettingGroup Group, string Title)[] Groups =
    [
        (SettingGroup.Digest, "📰 Дайджест"),
        (SettingGroup.Alerts, "🚨 Алерти"),
        (SettingGroup.Collecting, "📥 Збір"),
        (SettingGroup.Chat, "💬 Чат"),
        (SettingGroup.Models, "🧠 Моделі"),
    ];

    public static Screen Home(bool overridesIgnored)
    {
        var html = SettingsMenuTexts.MainTitle + (overridesIgnored ? "\n\n" + SettingsMenuTexts.OverridesIgnored : string.Empty);
        List<IReadOnlyList<KeyboardButton>> rows = [[Button("📡 Джерела", MenuAction.OpenSources)]];
        rows.AddRange(Groups.Chunk(2).Select(pair => (IReadOnlyList<KeyboardButton>)pair.Select(g => Button(g.Title, MenuAction.OpenGroup(g.Group))).ToList()));
        return new Screen(html, new InlineKeyboard(rows));
    }

    public static Screen Group(SettingGroup group, AgentOptions effective, SettingsOverrides overrides, string? note = null)
    {
        var definitions = SettingsCatalog.InGroup(group);
        var title = Groups.FirstOrDefault(g => g.Group == group).Title ?? group.ToString();
        var lines = definitions.Select(d =>
            $"{TelegramHtml.Escape(d.Label)}: <b>{TelegramHtml.Escape(Display(d, effective))}</b>{(overrides.Values.ContainsKey(d.Key) ? " ✎" : string.Empty)}");
        var html = $"<b>{title}</b>" + (note is null ? string.Empty : "\n" + note) + "\n\n" + string.Join('\n', lines);

        List<IReadOnlyList<KeyboardButton>> rows = [];
        foreach (var d in definitions)
        {
            rows.Add(d.Kind switch
            {
                SettingKind.Int => IntRow(d, CurrentInt(d, effective)),
                SettingKind.Choice => [Button($"{d.Label}: {Display(d, effective)}", MenuAction.OpenChoice(d.Key))],
                _ => [Button($"{d.Label}: {Display(d, effective)}", MenuAction.AskText(d.Key))],
            });
        }

        var changed = definitions.Where(d => overrides.Values.ContainsKey(d.Key)).ToList();
        rows.AddRange(changed.Select(d => (IReadOnlyList<KeyboardButton>)[Button($"↺ {d.Label}", MenuAction.Reset(d.Key))]));
        if (changed.Count > 1)
        {
            rows.Add([Button("↺ Скинути все в групі", MenuAction.ResetGroup(group))]);
        }

        rows.Add([Button("⬅ Назад", MenuAction.Main)]);
        return new Screen(html, new InlineKeyboard(rows));
    }

    public static Screen Choice(SettingDefinition definition, AgentOptions effective)
    {
        var current = Raw(definition, effective);
        var onList = definition.Choices.Any(c => c.Value == current);
        var html = $"<b>{TelegramHtml.Escape(definition.Label)}</b>\nПоточне: <b>{TelegramHtml.Escape(current)}</b>"
            + (onList ? string.Empty : "\n" + SettingsMenuTexts.NotOnTheList);

        List<IReadOnlyList<KeyboardButton>> rows = definition.Choices
            .Select((c, i) => (IReadOnlyList<KeyboardButton>)[Button((c.Value == current ? "✅ " : string.Empty) + c.Label, MenuAction.Choose(definition.Key, i))])
            .ToList();
        rows.Add([Button("⬅ Назад", MenuAction.OpenGroup(definition.Group))]);
        return new Screen(html, new InlineKeyboard(rows));
    }

    public static Screen Ask(SettingDefinition definition, string? error)
    {
        var html = $"<b>{TelegramHtml.Escape(definition.Label)}</b> (<code>{definition.Key}</code>)\n"
            + (definition.Kind == SettingKind.QuietHours ? SettingsMenuTexts.AskQuietHours : SettingsMenuTexts.AskValue)
            + (error is null ? string.Empty : "\n\n⚠️ " + TelegramHtml.Escape(error));
        return new Screen(html, new InlineKeyboard([[Button("✖ Скасувати", MenuAction.Cancel)]]));
    }

    public static Screen AskSource(string? error) =>
        new($"<b>📡 Нове джерело</b>\n{SettingsMenuTexts.AskSource}" + (error is null ? string.Empty : "\n\n⚠️ " + TelegramHtml.Escape(error)),
            new InlineKeyboard([[Button("✖ Скасувати", MenuAction.Cancel)]]));

    public static Screen Sources(IReadOnlyList<SourceEntry> sources, IReadOnlyCollection<string> alertSources, string? note = null)
    {
        var lines = sources.Select(s => $"{(s.Verified ? "✅" : "⏳")} @{TelegramHtml.Escape(s.Name)}{(alertSources.Contains(s.Name) ? " 🚨" : string.Empty)}");
        var html = "<b>📡 Джерела</b>" + (note is null ? string.Empty : "\n" + note) + "\n\n" + string.Join('\n', lines) + "\n\n" + SettingsMenuTexts.SourcesLegend;

        List<IReadOnlyList<KeyboardButton>> rows = sources
            .Select(s => (IReadOnlyList<KeyboardButton>)
            [
                Button($"🚨 {s.Name}", MenuAction.ToggleAlert(s.Name)),
                Button($"🗑 {s.Name}", MenuAction.RemoveSource(s.Name)),
            ])
            .ToList();
        rows.Add([Button("➕ Додати", MenuAction.AddSource)]);
        rows.Add([Button("⬅ Назад", MenuAction.Main)]);
        return new Screen(html, new InlineKeyboard(rows));
    }

    // Large ranges step in larger jumps, otherwise 4000 characters would need thousands of presses.
    public static int Step(SettingDefinition d) => (d.Max - d.Min) switch
    {
        >= 10000 => 500,
        >= 1000 => 100,
        >= 90 => 5,
        _ => 1,
    };

    private static IReadOnlyList<KeyboardButton> IntRow(SettingDefinition d, int current)
    {
        var step = Step(d);
        return
        [
            Button($"− {d.Label}", MenuAction.SetInt(d.Key, Math.Max(d.Min, current - step))),
            Button($"+ {d.Label}", MenuAction.SetInt(d.Key, Math.Min(d.Max, current + step))),
        ];
    }

    private static int CurrentInt(SettingDefinition d, AgentOptions options) =>
        int.TryParse(Raw(d, options), out var value) ? value : d.Min;

    private static string Raw(SettingDefinition d, AgentOptions options) =>
        typeof(AgentOptions).GetProperty(d.Key)?.GetValue(options)?.ToString() ?? string.Empty;

    private static string Display(SettingDefinition d, AgentOptions options)
    {
        var raw = Raw(d, options);
        return raw.Length > 0 ? raw : d.Kind == SettingKind.QuietHours ? "вимкнено" : "—";
    }

    private static KeyboardButton Button(string text, MenuAction action) => new(text, CallbackData.Encode(action));
}
