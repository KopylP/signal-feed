using Microsoft.Extensions.Options;
using PostAgent.Alerts;
using PostAgent.Shared.Common;

namespace PostAgent.Shared.Settings;

public sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    private const string UnresolvedKeyVaultReference = "@Microsoft.KeyVault(";

    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var errors = new List<string>();

        errors.AddIf(options.SourceList.Count == 0, "Sources must contain at least one channel or group username.");
        errors.AddIf(options.TelegramChatId == 0, "TelegramChatId is required: the numeric id of the chat that receives the digest.");
        errors.AddIfNotNull(QuietHours.Validate(options.AlertQuietHours, options.AlertTimeZone));

        // AlertFunction шукає пости за вікно плюс cooldown, а Cleanup зберігає лише RetentionDays.
        errors.AddIf(
            options.AlertWindowHours + options.AlertCooldownHours > options.RetentionDays * 24,
            "AlertWindowHours + AlertCooldownHours must not exceed RetentionDays * 24: older posts are already deleted.");

        errors.AddRange(UnresolvedSecrets(options).Select(secret =>
            $"{secret}: the Key Vault reference is not resolved. Set the secret, check the role and restart the app."));

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

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
