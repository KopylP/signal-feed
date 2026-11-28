using PostAgent.Shared.Ai;
using PostAgent.Shared.Notifications;

namespace PostAgent.Shared.Settings;

// The texts and buttons of a proposal. Built here from the structured changes and the real current values, never from the model's words:
// a post that talks the model into proposing something can only produce a change that is shown to the owner as it is.
public static class SettingsProposalMessage
{
    public static string Proposed(SettingsProposal proposal, AgentOptions current) =>
        "⚙️ <b>Агент пропонує змінити налаштування</b>\n\n" + Lines(proposal, current) + "\n\nЗастосувати?";

    // The lines each rewrite in the plan changes, as messages sent before the confirmation: a rewrite is far longer than the 4096 characters of one message.
    // promptSections: what is in force now, so the owner sees the change against the text the prompt really has.
    public static IReadOnlyList<string> RewriteDiffs(SettingsProposal proposal, IReadOnlyDictionary<string, string>? promptSections)
    {
        var messages = new List<string>();
        foreach (var change in proposal.Changes.Where(c => c.Kind == SettingChangeKind.RewritePrompt))
        {
            var label = Shipped.Value.FindSection(change.Target)?.Label ?? change.Target;
            var before = Shipped.Value.Text(change.Target, promptSections);
            var after = TextEdit.Normalize(change.Value);
            var changes = LineDiff.Compare(before, after);
            var header = $"📝 <b>Промпт «{TelegramHtml.Escape(label)}»</b>: {before.Length} → {after.Length} символів" + (changes.Count == 0 ? ", без змін" : string.Empty);
            messages.AddRange(Pack(header, changes.SelectMany(Render)));
        }

        return messages;
    }

    private static IEnumerable<string> Render((LineDiff.Mark Mark, string Line) change)
    {
        if (change.Mark == LineDiff.Mark.Gap)
        {
            yield return "…";
            yield break;
        }

        var sign = change.Mark == LineDiff.Mark.Removed ? "−" : "+";
        if (change.Line.Length == 0)
        {
            yield return $"{sign} <i>(порожній рядок)</i>";
            yield break;
        }

        // A long line is cut so that no message ends inside a tag and each piece is a small message of its own.
        foreach (var piece in change.Line.Chunk(MaxLineChars))
        {
            yield return $"{sign} <code>{TelegramHtml.Escape(new string(piece))}</code>";
        }
    }

    private static IEnumerable<string> Pack(string header, IEnumerable<string> lines)
    {
        var message = new System.Text.StringBuilder(header);
        foreach (var line in lines)
        {
            if (message.Length + line.Length + 1 > MaxMessageChars)
            {
                yield return message.ToString();
                message.Clear();
                message.Append(line);
                continue;
            }

            message.Append('\n').Append(line);
        }

        yield return message.ToString();
    }

    // Under Telegram's 4096 with room for the escaping of "<", ">" and "&" that the cut text still gets.
    private const int MaxMessageChars = 3000;
    private const int MaxLineChars = 600;

    public static InlineKeyboard Keyboard(SettingsProposal proposal) =>
        new([[new KeyboardButton("✅ Застосувати", MenuCallbacks.ApplyProposal(proposal.Id), ButtonStyle.Success), new KeyboardButton("✖ Скасувати", MenuCallbacks.DeclineProposal(proposal.Id), ButtonStyle.Danger)]]);

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

            case SettingChangeKind.AddWatch:
                return $"Стежити за темою <b>«{target}»</b>: новий пост з усіма цими словами прийде окремим алертом";

            case SettingChangeKind.RemoveWatch:
                return $"Припинити стежити за темою <b>«{target}»</b>";

            case SettingChangeKind.EditPrompt:
                var label = Shipped.Value.FindSection(change.Target)?.Label ?? change.Target;
                return $"Промпт «{TelegramHtml.Escape(label)}»:\n{Diff(change)}";

            case SettingChangeKind.RewritePrompt:
                var rewritten = Shipped.Value.FindSection(change.Target)?.Label ?? change.Target;
                var length = TextEdit.Normalize(change.Value).Length;
                return $"Промпт «{TelegramHtml.Escape(rewritten)}»: " + (current is null ? $"секцію переписано ({length} символів)" : $"переписати секцію повністю ({length} символів), зміни вище");

            case SettingChangeKind.ResetPrompt:
                return $"Промпт «{TelegramHtml.Escape(Shipped.Value.FindSection(change.Target)?.Label ?? change.Target)}»: повернути стандартний текст";

            case SettingChangeKind.ClearContext:
                return "Очистити контекст розмови: питання й відповіді буде забуто, дайджест лишається";

            default:
                return target;
        }
    }

    // The exact fragments, built from the stored change and never from the model's words. The sections of the shipped prompts are fixed, so the label comes from them.
    private static readonly Lazy<PromptLibrary> Shipped = new(() => new PromptLibrary());

    private static string Diff(SettingChange change)
    {
        var before = string.IsNullOrWhiteSpace(change.Before) ? null : $"− <code>{TelegramHtml.Escape(change.Before.Trim())}</code>";
        var after = string.IsNullOrWhiteSpace(change.Value) ? "+ (видалити)" : $"+ <code>{TelegramHtml.Escape(change.Value.Trim())}</code>";
        return (before ?? "(додати в кінець)") + "\n" + after;
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
