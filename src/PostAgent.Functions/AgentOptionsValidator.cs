using Microsoft.Extensions.Options;

namespace PostAgent;

/// <summary>Перевірки, які не виразити атрибутами: узгодженість налаштувань між собою.</summary>
public sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var errors = new List<string>();

        if (QuietHours.Validate(options.AlertQuietHours, options.AlertTimeZone) is { } quietHoursError)
        {
            errors.Add(quietHoursError);
        }

        // AlertFunction шукає пости за вікно плюс cooldown, а Cleanup зберігає лише RetentionDays.
        if (options.AlertWindowHours + options.AlertCooldownHours > options.RetentionDays * 24)
        {
            errors.Add("AlertWindowHours + AlertCooldownHours must not exceed RetentionDays * 24: older posts are already deleted.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
