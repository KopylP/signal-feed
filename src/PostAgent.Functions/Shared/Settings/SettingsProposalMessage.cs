using PostAgent.Shared.Notifications;

namespace PostAgent.Shared.Settings;

// The texts and buttons of a proposal. Built here from the structured changes and the real current values, never from the model's words:
// a post that talks the model into proposing something can only produce a change that is shown to the owner as it is.
public static class SettingsProposalMessage
{
    public static string Proposed(SettingsProposal proposal, AgentOptions current) =>
        "⚙️ <b>Агент пропонує змінити налаштування</b>\n\n" + Lines(proposal, current) + "\n\nЗастосувати?";

    public static InlineKeyboard Keyboard(SettingsProposal proposal) =>
        new([[new KeyboardButton("✅ Застосувати", MenuCallbacks.ApplyProposal(proposal.Id)), new KeyboardButton("✖ Скасувати", MenuCallbacks.DeclineProposal(proposal.Id))]]);

    public static string Applied(SettingsProposal proposal) => "✅ <b>Застосовано</b>\n\n" + Lines(proposal, current: null);

    public const string Declined = "✖ Пропозицію скасовано.";

    public const string Expired = "⌛ Пропозиція застаріла. Попроси агента ще раз.";

    public static string Failed(string? error) => "⚠️ Не вдалося застосувати: " + TelegramHtml.Escape(error ?? "помилка збереження") + "\nНічого не змінено.";

    public static string NotSaved(string? error) => "⚠️ Не вдалося підготувати пропозицію: " + TelegramHtml.Escape(error ?? "помилка збереження");

    private static string Lines(SettingsProposal proposal, AgentOptions? current) =>
        string.Join('\n', proposal.Changes.Select(c => "• " + Describe(c, current)));

    private static string Describe(SettingChange change, AgentOptions? current)
    {
        var target = TelegramHtml.Escape(change.Target);
        switch (change.Kind)
        {
            case SettingChangeKind.Set when SettingsCatalog.Find(change.Target) is { } definition:
                var next = SettingsCatalog.Display(definition, change.Value ?? string.Empty);
                var before = current is null ? string.Empty : $"{TelegramHtml.Escape(SettingsCatalog.Display(definition, SettingsCatalog.Raw(definition, current)))} → ";
                return $"{TelegramHtml.Escape(definition.Label)}: {before}<b>{TelegramHtml.Escape(next)}</b>";

            case SettingChangeKind.Reset when SettingsCatalog.Find(change.Target) is { } definition:
                return $"{TelegramHtml.Escape(definition.Label)}: повернути значення з App Settings";

            case SettingChangeKind.ResetGroup:
                return $"Повернути значення з App Settings для групи «{TelegramHtml.Escape(GroupTitle(change.Target))}»";

            case SettingChangeKind.AddSource:
                return $"Додати джерело <b>@{target}</b>";

            case SettingChangeKind.RemoveSource:
                return $"Видалити джерело <b>@{target}</b>";

            case SettingChangeKind.ToggleAlertSource:
                return $"Перемкнути алерти для <b>@{target}</b>";

            default:
                return target;
        }
    }

    private static string GroupTitle(string group) => group switch
    {
        nameof(SettingGroup.Sources) => "Джерела",
        nameof(SettingGroup.Digest) => "Дайджест",
        nameof(SettingGroup.Alerts) => "Алерти",
        nameof(SettingGroup.Collecting) => "Збір",
        nameof(SettingGroup.Chat) => "Чат",
        nameof(SettingGroup.Models) => "Моделі",
        _ => group,
    };
}
