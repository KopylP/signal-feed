using Microsoft.Extensions.Options;

namespace PostAgent.Tests;

public class AgentOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<AgentOptions> configure)
    {
        var options = new AgentOptions();
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
}
