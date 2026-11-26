using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class AgentOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<AgentOptions> configure)
    {
        var options = new AgentOptions
        {
            Sources = "news",
            TelegramPairingKey = "pairing-key-0123456789",
            TelegramWebhookSecret = "webhook_secret-token",
        };
        configure(options);
        return new AgentOptionsValidator().Validate(null, options);
    }

    [Fact]
    public void Default_settings_are_valid()
    {
        Assert.True(Validate(_ => { }).Succeeded);
    }

    [Fact]
    public void Malformed_quiet_hours_are_reported_at_startup_instead_of_failing_every_hour()
    {
        var result = Validate(o => o.AlertQuietHours = "23-07");

        Assert.True(result.Failed);
        Assert.Contains("AlertQuietHours", result.FailureMessage);
    }

    [Fact]
    public void Unknown_time_zone_is_reported()
    {
        var result = Validate(o =>
        {
            o.AlertQuietHours = "23:00-07:00";
            o.AlertTimeZone = "Europe/Nowhere";
        });

        Assert.Contains("AlertTimeZone", result.FailureMessage);
    }

    [Fact]
    public void Unknown_reader_time_zone_is_reported()
    {
        var result = Validate(o => o.TimeZone = "Europe/Nowhere");

        Assert.Contains("TimeZone", result.FailureMessage);
        Assert.DoesNotContain("AlertTimeZone", result.FailureMessage);
    }

    [Fact]
    public void Alert_lookback_longer_than_the_retained_data_is_rejected()
    {
        var result = Validate(o =>
        {
            o.AlertWindowHours = 20;
            o.AlertCooldownHours = 6;
            o.RetentionDays = 1;
        });

        Assert.Contains("RetentionDays", result.FailureMessage);
    }

    [Fact]
    public void Empty_source_list_is_rejected_even_when_the_setting_is_not_blank()
    {
        var result = Validate(o => o.Sources = " , ");

        Assert.Contains("Sources", result.FailureMessage);
    }

    [Fact]
    public void Chat_id_is_optional_because_pairing_can_provide_it()
    {
        Assert.True(Validate(o => o.TelegramChatId = 0).Succeeded);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has space in it 0123456789")]
    [InlineData("слово-слово-слово-слово")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public void A_pairing_key_that_is_short_long_or_not_deep_link_safe_is_rejected(string key)
    {
        var result = Validate(o => o.TelegramPairingKey = key);

        Assert.Contains("TelegramPairingKey", result.FailureMessage);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("слово")]
    [InlineData("semi;colon")]
    public void A_webhook_secret_outside_the_telegram_alphabet_is_rejected(string secret)
    {
        var result = Validate(o => o.TelegramWebhookSecret = secret);

        Assert.Contains("TelegramWebhookSecret", result.FailureMessage);
    }

    [Theory]
    [InlineData(nameof(AgentOptions.TelegramPairingKey))]
    [InlineData(nameof(AgentOptions.TelegramWebhookSecret))]
    public void Unresolved_key_vault_reference_of_a_pairing_secret_is_reported(string setting)
    {
        var reference = "@Microsoft.KeyVault(SecretUri=https://kv.vault.azure.net/secrets/x/)";

        var result = Validate(o => typeof(AgentOptions).GetProperty(setting)!.SetValue(o, reference));

        Assert.Contains(setting, result.FailureMessage);
    }

    [Fact]
    public void Unresolved_key_vault_reference_is_reported_by_secret_name()
    {
        var result = Validate(o => o.OpenAIApiKey = "@Microsoft.KeyVault(SecretUri=https://kv.vault.azure.net/secrets/OpenAIApiKey/)");

        Assert.Contains("OpenAIApiKey", result.FailureMessage);
        Assert.Contains("Key Vault", result.FailureMessage);
    }
}
