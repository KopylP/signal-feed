using Microsoft.Extensions.Options;
using PostAgent.Settings;

namespace PostAgent.Tests.Settings;

public class AgentOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<AgentOptions> configure)
    {
        var options = new AgentOptions { Sources = "news", TelegramChatId = 1 };
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
    public void Missing_chat_id_is_rejected()
    {
        var result = Validate(o => o.TelegramChatId = 0);

        Assert.Contains("TelegramChatId", result.FailureMessage);
    }

    [Fact]
    public void Unresolved_key_vault_reference_is_reported_by_secret_name()
    {
        var result = Validate(o => o.OpenAIApiKey = "@Microsoft.KeyVault(SecretUri=https://kv.vault.azure.net/secrets/OpenAIApiKey/)");

        Assert.Contains("OpenAIApiKey", result.FailureMessage);
        Assert.Contains("Key Vault", result.FailureMessage);
    }
}
