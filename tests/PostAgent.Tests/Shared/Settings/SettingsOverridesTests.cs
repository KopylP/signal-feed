using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsOverridesTests
{
    private static readonly AgentOptions App = OverridesApplierTests.ValidApp(o =>
    {
        o.AlertSources = "chat";
        o.Watches = "ai";
    });

    [Fact]
    public void Without_overrides_the_app_setting_lists_are_in_force_and_its_sources_count_as_verified()
    {
        var overrides = SettingsOverrides.Empty;

        Assert.Equal([new SourceEntry("news", true), new SourceEntry("chat", true)], overrides.EffectiveSources(App));
        Assert.Equal(["chat"], overrides.EffectiveAlertSources(App));
        Assert.Equal(["ai"], overrides.EffectiveWatches(App));
    }

    [Fact]
    public void The_bot_lists_win_over_the_app_settings_even_when_empty()
    {
        var overrides = SettingsOverrides.Empty with { Sources = [new SourceEntry("fresh", false)], AlertSources = [], Watches = [] };

        Assert.Equal([new SourceEntry("fresh", false)], overrides.EffectiveSources(App));
        Assert.Empty(overrides.EffectiveAlertSources(App));
        Assert.Empty(overrides.EffectiveWatches(App));
    }
}
