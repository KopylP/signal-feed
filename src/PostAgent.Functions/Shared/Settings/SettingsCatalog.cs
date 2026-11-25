using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace PostAgent.Shared.Settings;

// The parameters the owner can change from the Telegram bot. Sources and AlertSources are not here: the sources screen manages them.
// Secrets, schedules and infrastructure values are never editable this way.
public static class SettingsCatalog
{
    public static readonly IReadOnlyList<SettingChoice> Models =
    [
        new("gpt-5.4-nano", "gpt-5.4-nano — дешева"),
        new("gpt-6.1-terra", "gpt-6.1-terra — розумніша"),
        new("gpt-6.1-sol", "gpt-6.1-sol — найдорожча"),
    ];

    private static readonly IReadOnlyList<SettingChoice> ReasoningEfforts =
    [
        new("none", "без міркувань"),
        new("low", "мало"),
        new("medium", "середньо"),
        new("high", "багато"),
    ];

    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        Int("DigestSize", SettingGroup.Digest, "Постів у дайджесті"),
        Int("InterestingThreshold", SettingGroup.Digest, "Поріг «цікаве»"),

        Int("AlertThreshold", SettingGroup.Alerts, "Поріг алерта"),
        Int("BurstMinPosts", SettingGroup.Alerts, "Мін. постів для сплеску"),
        Int("AlertWindowHours", SettingGroup.Alerts, "Вікно алерта, год"),
        Int("AlertCooldownHours", SettingGroup.Alerts, "Пауза між алертами, год"),
        Int("AlertMaxPerRun", SettingGroup.Alerts, "Записів в алерті"),
        Other("AlertQuietHours", SettingGroup.Alerts, SettingKind.QuietHours, "Тихі години"),
        Other("AlertTimeZone", SettingGroup.Alerts, SettingKind.Text, "Часовий пояс"),

        Int("MinPostChars", SettingGroup.Collecting, "Мін. довжина посту"),
        Int("MaxPostChars", SettingGroup.Collecting, "Макс. довжина посту"),
        Int("MaxItemsPerGroup", SettingGroup.Collecting, "Постів на групу за запуск"),
        Int("MaxBundleChars", SettingGroup.Collecting, "Розмір пачки, символів"),
        Int("ScoringBatchSize", SettingGroup.Collecting, "Постів у пачці оцінювання"),

        Choice("OpenAIChatModel", SettingGroup.Chat, "Модель чату", Models),
        Choice("ChatReasoningEffort", SettingGroup.Chat, "Міркування чату", ReasoningEfforts),
        Int("ChatMaxToolCalls", SettingGroup.Chat, "Викликів інструментів на питання"),
        Int("ChatMaxMessages", SettingGroup.Chat, "Повідомлень у розмові"),

        Choice("OpenAIModel", SettingGroup.Models, "Модель оцінювання і дайджесту", Models),
    ];

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(d => d.Key == key);

    public static IReadOnlyList<SettingDefinition> InGroup(SettingGroup group) => All.Where(d => d.Group == group).ToList();

    // The bounds have one home: the Range attribute of the AgentOptions property.
    private static SettingDefinition Int(string key, SettingGroup group, string label)
    {
        var range = typeof(AgentOptions).GetProperty(key)?.GetCustomAttribute<RangeAttribute>()
            ?? throw new InvalidOperationException($"{key} has no Range attribute.");
        return new SettingDefinition(key, group, SettingKind.Int, label, (int)range.Minimum, (int)range.Maximum, []);
    }

    private static SettingDefinition Choice(string key, SettingGroup group, string label, IReadOnlyList<SettingChoice> choices) =>
        new(key, group, SettingKind.Choice, label, 0, 0, choices);

    private static SettingDefinition Other(string key, SettingGroup group, SettingKind kind, string label) =>
        new(key, group, kind, label, 0, 0, []);
}
