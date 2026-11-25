using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class OverridesApplierTests
{
    internal static AgentOptions ValidApp(Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions
        {
            Sources = "news,chat",
            OpenAIApiKey = "sk-test",
            TelegramBotToken = "bot-token",
            TelegramPairingKey = "pairing-key-0123456789",
            TelegramWebhookSecret = "webhook_secret-token",
            TelegramApiId = 1,
            TelegramApiHash = "hash",
        };
        configure?.Invoke(options);
        return options;
    }

    private static SettingsOverrides With(params (string Key, string Value)[] values) =>
        SettingsOverrides.Empty with { Values = values.ToDictionary(v => v.Key, v => v.Value) };

    [Fact]
    public void Applies_a_number_a_choice_and_the_source_lists()
    {
        var overrides = With(("DigestSize", "9"), ("ChatReasoningEffort", "high")) with
        {
            Sources = [new SourceEntry("one", true), new SourceEntry("two", false)],
            AlertSources = ["two"],
        };

        var result = OverridesApplier.Apply(ValidApp(), overrides);

        Assert.Equal(9, result.DigestSize);
        Assert.Equal("high", result.ChatReasoningEffort);
        Assert.Equal(["one", "two"], result.SourceList);
        Assert.Equal(["two"], result.AlertSourceSet);
    }

    [Fact]
    public void Keeps_the_app_setting_when_the_key_is_not_overridden()
    {
        var app = ValidApp(o => o.DigestSize = 5);

        var result = OverridesApplier.Apply(app, SettingsOverrides.Empty);

        Assert.Equal(5, result.DigestSize);
        Assert.Equal(["news", "chat"], result.SourceList);
    }

    [Fact]
    public void Does_not_change_the_app_options_object()
    {
        var app = ValidApp();

        OverridesApplier.Apply(app, With(("DigestSize", "9")));

        Assert.Equal(7, app.DigestSize);
    }

    [Fact]
    public void Skips_an_unknown_key_and_an_unparsable_number()
    {
        var result = OverridesApplier.Apply(ValidApp(), With(("DigestSize", "many"), ("OpenAIApiKey", "stolen"), ("Nope", "1")));

        Assert.Equal(7, result.DigestSize);
        Assert.Equal("sk-test", result.OpenAIApiKey);
    }

    [Fact]
    public void Check_accepts_valid_options()
    {
        Assert.Empty(OverridesApplier.Check(ValidApp()));
    }

    [Fact]
    public void Check_rejects_a_number_out_of_range_and_names_the_field()
    {
        var errors = OverridesApplier.Check(ValidApp(o => o.DigestSize = 13));

        Assert.Contains(errors, e => e.Contains("DigestSize"));
    }

    [Fact]
    public void Check_names_both_fields_when_window_plus_cooldown_exceeds_retention()
    {
        var errors = OverridesApplier.Check(ValidApp(o =>
        {
            o.AlertWindowHours = 20;
            o.AlertCooldownHours = 10;
            o.RetentionDays = 1;
        }));

        Assert.Contains(errors, e => e.Contains("AlertWindowHours") && e.Contains("AlertCooldownHours"));
    }

    [Fact]
    public void Check_rejects_an_unknown_reasoning_effort()
    {
        Assert.NotEmpty(OverridesApplier.Check(ValidApp(o => o.ChatReasoningEffort = "extreme")));
    }
}
