using System.ComponentModel.DataAnnotations;

namespace PostAgent.Shared.Settings;

public static class OverridesApplier
{
    // Returns a copy of the App Setting options with the overrides on top. A key that is not in the catalog (a secret, say)
    // or a value that cannot be parsed is skipped, so a damaged blob can never touch what the bot may not change.
    public static AgentOptions Apply(AgentOptions app, SettingsOverrides overrides)
    {
        var result = app.Clone();

        foreach (var (key, value) in overrides.Values)
        {
            if (SettingsCatalog.Find(key) is not { } definition || typeof(AgentOptions).GetProperty(key) is not { } property)
            {
                continue;
            }

            if (definition.Kind == SettingKind.Int)
            {
                if (int.TryParse(value, out var number))
                {
                    property.SetValue(result, number);
                }
            }
            else
            {
                property.SetValue(result, value);
            }
        }

        if (overrides.Sources is not null)
        {
            result.Sources = string.Join(',', overrides.Sources.Select(s => s.Name));
        }

        if (overrides.AlertSources is not null)
        {
            result.AlertSources = string.Join(',', overrides.AlertSources);
        }

        if (overrides.Watches is not null)
        {
            result.Watches = WatchPhrases.Join(overrides.Watches);
        }

        return result;
    }

    // The messages name the failed fields. Empty means the options are valid. Used before a value from the bot is saved.
    public static IReadOnlyList<string> Check(AgentOptions candidate)
    {
        var errors = new List<string>();

        var annotations = new List<ValidationResult>();
        Validator.TryValidateObject(candidate, new ValidationContext(candidate), annotations, validateAllProperties: true);
        errors.AddRange(annotations.Select(a => a.ErrorMessage ?? "Invalid value."));

        if (new AgentOptionsValidator().Validate(null, candidate) is { Failed: true } failed)
        {
            errors.AddRange(failed.Failures ?? []);
        }

        return errors;
    }
}
