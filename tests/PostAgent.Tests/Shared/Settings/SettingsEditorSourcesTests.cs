using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsEditorSourcesTests
{
    private readonly InMemoryOverridesStore _store = new();

    private SettingsEditor Create(Action<AgentOptions>? configure = null)
    {
        var options = Options.Create(OverridesApplierTests.ValidApp(configure));
        var effective = new EffectiveOptions(_store, options, new FakeTimeProvider(TestData.Now), NullLogger<EffectiveOptions>.Instance);
        return new SettingsEditor(_store, options, effective, DefaultPrompts.Library, new InMemoryProfileStore(), new InMemoryChatStateStore(), NullLogger<SettingsEditor>.Instance);
    }

    [Fact]
    public async Task Adds_a_new_source_as_not_verified_and_keeps_the_app_setting_sources_as_verified()
    {
        var result = await Create().AddSourceAsync("fresh", CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal([new SourceEntry("news", true), new SourceEntry("chat", true), new SourceEntry("fresh", false)], _store.Current?.Sources);
    }

    [Fact]
    public async Task Ignores_a_duplicate()
    {
        await Create().AddSourceAsync("news", CancellationToken.None);

        Assert.Null(_store.Current);
    }

    [Fact]
    public async Task Refuses_to_remove_the_last_source()
    {
        var editor = Create(o => o.Sources = "only");

        var result = await editor.RemoveSourceAsync("only", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Null(_store.Current);
    }

    [Fact]
    public async Task Removing_a_source_removes_it_from_the_alert_list()
    {
        var editor = Create(o => o.AlertSources = "chat");

        await editor.RemoveSourceAsync("chat", CancellationToken.None);

        Assert.Equal(["news"], _store.Current?.Sources?.Select(s => s.Name));
        Assert.Empty(_store.Current?.AlertSources!);
    }

    [Fact]
    public async Task Removing_a_missing_source_is_not_an_error()
    {
        var result = await Create().RemoveSourceAsync("gone", CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Null(_store.Current);
    }

    [Fact]
    public async Task Toggles_an_alert_source_on_and_off()
    {
        var editor = Create();

        await editor.ToggleAlertSourceAsync("chat", CancellationToken.None);
        Assert.Equal(["chat"], _store.Current?.AlertSources);

        await editor.ToggleAlertSourceAsync("chat", CancellationToken.None);
        Assert.Empty(_store.Current?.AlertSources!);
    }

    [Fact]
    public async Task Does_not_make_an_unknown_source_an_alert_source()
    {
        await Create().ToggleAlertSourceAsync("unknown", CancellationToken.None);

        Assert.Null(_store.Current);
    }

    [Fact]
    public async Task Lists_the_unverified_sources()
    {
        _store.Seed(SettingsOverrides.Empty with { Sources = [new SourceEntry("old", true), new SourceEntry("fresh", false)] });

        Assert.Equal(["fresh"], await Create().UnverifiedSourcesAsync(CancellationToken.None));
    }
}
