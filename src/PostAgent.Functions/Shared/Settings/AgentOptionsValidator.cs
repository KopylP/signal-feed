using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;

namespace PostAgent.Shared.Settings;

public sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    private const string UnresolvedKeyVaultReference = "@Microsoft.KeyVault(";

    // The deep link t.me/<bot>?start=<key> only carries these characters, up to 64 of them.
    private static readonly Regex PairingKeyPattern = new("^[A-Za-z0-9_-]{16,64}$", RegexOptions.CultureInvariant);

    private static readonly Regex WebhookSecretPattern = new("^[A-Za-z0-9_-]{1,256}$", RegexOptions.CultureInvariant);

    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var errors = new List<string>();

        errors.AddIf(options.SourceList.Count == 0, "Sources must contain at least one channel or group username.");
        errors.AddIf(
            options.TelegramPairingKey.Length > 0 && !PairingKeyPattern.IsMatch(options.TelegramPairingKey),
            "TelegramPairingKey must be 16-64 characters from A-Z, a-z, 0-9, underscore and hyphen: it is sent in a t.me deep link, and anyone who guesses it can claim the agent.");
        errors.AddIf(
            options.TelegramWebhookSecret.Length > 0 && !WebhookSecretPattern.IsMatch(options.TelegramWebhookSecret),
            "TelegramWebhookSecret must be 1-256 characters from A-Z, a-z, 0-9, underscore and hyphen (a Telegram limit).");
        errors.AddIfNotNull(QuietHours.Validate(options.AlertQuietHours, options.AlertTimeZone));
        errors.AddIf(
            !TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _),
            $"TimeZone '{options.TimeZone}' was not found on this system. Use an IANA id such as Europe/Kyiv.");

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
            [nameof(AgentOptions.TelegramPairingKey)] = options.TelegramPairingKey,
            [nameof(AgentOptions.TelegramWebhookSecret)] = options.TelegramWebhookSecret,
        }
        .Where(secret => secret.Value.StartsWith(UnresolvedKeyVaultReference, StringComparison.Ordinal))
        .Select(secret => secret.Key);
}
