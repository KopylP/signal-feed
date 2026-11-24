using Microsoft.Extensions.Options;

namespace PostAgent;

/// <summary>Перевірки, які не виразити атрибутами: узгодженість налаштувань між собою і нерозв'язані посилання на секрети.</summary>
public sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    private const string UnresolvedKeyVaultReference = "@Microsoft.KeyVault(";

    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var errors = new List<string>();

        if (options.SourceList.Count == 0)
        {
            errors.Add("Sources must contain at least one channel or group username.");
        }

        if (options.TelegramChatId == 0)
        {
            errors.Add("TelegramChatId is required: the numeric id of the chat that receives the digest.");
        }

        if (QuietHours.Validate(options.AlertQuietHours, options.AlertTimeZone) is { } quietHoursError)
        {
            errors.Add(quietHoursError);
        }

        // AlertFunction шукає пости за вікно плюс cooldown, а Cleanup зберігає лише RetentionDays.
        if (options.AlertWindowHours + options.AlertCooldownHours > options.RetentionDays * 24)
        {
            errors.Add("AlertWindowHours + AlertCooldownHours must not exceed RetentionDays * 24: older posts are already deleted.");
        }

        errors.AddRange(UnresolvedSecrets(options).Select(secret =>
            $"{secret}: the Key Vault reference is not resolved. Set the secret, check the role and restart the app."));

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    /// <summary>Нерозв'язане посилання на Key Vault надходить як буквальний рядок, тож проходить [Required].</summary>
    private static IEnumerable<string> UnresolvedSecrets(AgentOptions options) =>
        new Dictionary<string, string>
        {
            [nameof(AgentOptions.OpenAIApiKey)] = options.OpenAIApiKey,
            [nameof(AgentOptions.TelegramBotToken)] = options.TelegramBotToken,
            [nameof(AgentOptions.TelegramApiHash)] = options.TelegramApiHash,
        }
        .Where(secret => secret.Value.StartsWith(UnresolvedKeyVaultReference, StringComparison.Ordinal))
        .Select(secret => secret.Key);
}
