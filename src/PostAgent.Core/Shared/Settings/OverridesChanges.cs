using Microsoft.Extensions.Options;
using PostAgent.Shared.Ai;

namespace PostAgent.Shared.Settings;

// What a SettingChange does to the stored overrides. No I/O: SettingsEditor reads, saves and retries around it.
public sealed class OverridesChanges(IOptions<AgentOptions> app, PromptLibrary prompts)
{
    // All or nothing, and the result is checked as a whole. Resets are not checked: they are the way out of a bad state.
    public bool TryApplyAll(SettingsOverrides current, IReadOnlyList<SettingChange> changes, out SettingsOverrides updated, out string? error)
    {
        updated = current;
        error = null;
        if (changes.Count(c => c.Kind is SettingChangeKind.EditPrompt or SettingChangeKind.RewritePrompt) > TextEdit.MaxEditsPerPlan)
        {
            error = $"Забагато правок тексту в одному плані (до {TextEdit.MaxEditsPerPlan}).";
            return false;
        }

        foreach (var change in changes)
        {
            updated = Apply(updated, change, out error);
            if (error is not null)
            {
                return false;
            }
        }

        // Only a step that changes the options is checked against them: a text edit must not fail on an unrelated rule, and a reset is the way out.
        var touchesOptions = changes.Any(c => c.Kind is SettingChangeKind.Set or SettingChangeKind.AddSource or SettingChangeKind.RemoveSource or SettingChangeKind.ToggleAlertSource
            or SettingChangeKind.AddWatch or SettingChangeKind.RemoveWatch);
        if (touchesOptions && Validate(updated) is { } problems)
        {
            error = problems;
            return false;
        }

        return true;
    }

    // One change applied to the stored overrides. The same instance comes back when there is nothing to write.
    public SettingsOverrides Apply(SettingsOverrides o, SettingChange change, out string? error)
    {
        error = null;
        switch (change.Kind)
        {
            case SettingChangeKind.Set:
                return SetValue(o, change.Target, change.Value ?? string.Empty, out error);

            case SettingChangeKind.Reset:
                return o with { Values = o.Values.Where(v => v.Key != change.Target).ToDictionary() };

            case SettingChangeKind.ResetGroup when Enum.TryParse<SettingGroup>(change.Target, out var group) && Enum.IsDefined(group):
                var keys = SettingsCatalog.InGroup(group).Select(d => d.Key).ToHashSet();
                return o with
                {
                    Values = o.Values.Where(v => !keys.Contains(v.Key)).ToDictionary(),
                    Sources = group == SettingGroup.Sources ? null : o.Sources,
                    AlertSources = group == SettingGroup.Sources ? null : o.AlertSources,
                };

            case SettingChangeKind.AddSource:
                var sources = o.EffectiveSources(app.Value);
                return sources.Any(s => s.Name == change.Target) ? o : o with { Sources = [.. sources, new SourceEntry(change.Target, false)] };

            case SettingChangeKind.RemoveSource:
                return RemoveSource(o, change.Target, out error);

            case SettingChangeKind.ToggleAlertSource:
                if (o.EffectiveSources(app.Value).All(s => s.Name != change.Target))
                {
                    return o;
                }

                var alerts = o.EffectiveAlertSources(app.Value);
                return o with { AlertSources = alerts.Contains(change.Target) ? alerts.Where(a => a != change.Target).ToList() : [.. alerts, change.Target] };

            case SettingChangeKind.AddWatch:
                return AddWatch(o, change.Target, out error);

            case SettingChangeKind.RemoveWatch:
                var watches = o.EffectiveWatches(app.Value);
                return watches.Any(w => w.Equals(change.Target, StringComparison.OrdinalIgnoreCase))
                    ? o with { Watches = watches.Where(w => !w.Equals(change.Target, StringComparison.OrdinalIgnoreCase)).ToList() }
                    : o;

            case SettingChangeKind.EditPrompt:
                return EditPrompt(o, change, out error);

            case SettingChangeKind.RewritePrompt:
                return RewritePrompt(o, change, out error);

            case SettingChangeKind.ResetPrompt:
                return o.PromptSections is { } sections && sections.ContainsKey(change.Target)
                    ? o with { PromptSections = WithoutEmpty(sections.Where(s => s.Key != change.Target).ToDictionary()) }
                    : o;

            case SettingChangeKind.ClearContext:
                // Not a setting: the conversation is its own blob, cleared by SettingsProposals.ApplyAsync.
                return o;

            default:
                error = "Невідома зміна.";
                return o;
        }
    }

    // The overrides on top of the App Settings must still make valid options. Null when they do.
    public string? Validate(SettingsOverrides overrides) =>
        OverridesApplier.Check(OverridesApplier.Apply(app.Value, overrides)) is { Count: > 0 } errors ? string.Join('\n', errors) : null;

    private SettingsOverrides EditPrompt(SettingsOverrides o, SettingChange change, out string? error)
    {
        error = null;
        if (prompts.FindSection(change.Target) is not { } section)
        {
            error = "Невідома секція промпта.";
            return o;
        }

        return TextEdit.TryApply(prompts.Text(section.Id, o.PromptSections), change.Before, change.Value, out var text, out error)
            ? StoreSection(o, section, text, out error)
            : o;
    }

    // The whole text of a section replaced at once; the owner saw the line diff of it before the tap.
    private SettingsOverrides RewritePrompt(SettingsOverrides o, SettingChange change, out string? error)
    {
        error = null;
        if (prompts.FindSection(change.Target) is not { } section)
        {
            error = "Невідома секція промпта.";
            return o;
        }

        var text = TextEdit.Normalize(change.Value);
        if (text.Length > TextEdit.MaxText)
        {
            error = $"Текст секції задовгий (до {TextEdit.MaxText} символів).";
            return o;
        }

        return StoreSection(o, section, text, out error);
    }

    // A section keeps its place in the prompt (no headings, no markers, never empty); a text equal to the default is not stored.
    private static SettingsOverrides StoreSection(SettingsOverrides o, PromptSection section, string text, out string? error)
    {
        error = TextEdit.CheckSection(text);
        if (error is not null)
        {
            return o;
        }

        var sections = new Dictionary<string, string>(o.PromptSections ?? new Dictionary<string, string>());
        if (text == section.Default)
        {
            sections.Remove(section.Id);
        }
        else
        {
            sections[section.Id] = text;
        }

        return o with { PromptSections = WithoutEmpty(sections) };
    }

    private static Dictionary<string, string>? WithoutEmpty(Dictionary<string, string> sections) => sections.Count == 0 ? null : sections;

    private static SettingsOverrides SetValue(SettingsOverrides o, string key, string value, out string? error)
    {
        error = null;
        if (SettingsCatalog.Find(key) is not { } definition)
        {
            error = "Невідомий параметр.";
            return o;
        }

        if (definition.Kind == SettingKind.Int && !int.TryParse(value, out _))
        {
            error = "Потрібне число.";
            return o;
        }

        if (definition.Kind == SettingKind.Choice && definition.Choices.All(c => c.Value != value))
        {
            error = "Такого варіанта немає.";
            return o;
        }

        return o with { Values = new Dictionary<string, string>(o.Values) { [key] = value } };
    }

    private SettingsOverrides AddWatch(SettingsOverrides o, string text, out string? error)
    {
        error = null;
        if (WatchPhrases.Normalize(text) is not { } phrase)
        {
            error = $"Тема має бути коротким текстом до {WatchPhrases.MaxChars} символів.";
            return o;
        }

        var watches = o.EffectiveWatches(app.Value);
        if (watches.Any(w => w.Equals(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return o;
        }

        if (watches.Count >= WatchPhrases.MaxCount)
        {
            error = $"Можна стежити щонайбільше за {WatchPhrases.MaxCount} тем: видаліть одну.";
            return o;
        }

        return o with { Watches = [.. watches, phrase] };
    }

    private SettingsOverrides RemoveSource(SettingsOverrides o, string name, out string? error)
    {
        error = null;
        var sources = o.EffectiveSources(app.Value);
        if (sources.All(s => s.Name != name))
        {
            return o;
        }

        if (sources.Count == 1)
        {
            error = "Не можна видалити останнє джерело.";
            return o;
        }

        var alerts = o.EffectiveAlertSources(app.Value);
        return o with
        {
            Sources = sources.Where(s => s.Name != name).ToList(),
            AlertSources = alerts.Contains(name) ? alerts.Where(a => a != name).ToList() : o.AlertSources,
        };
    }
}
