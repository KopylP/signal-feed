using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsEditorSafetyTests
{
    private readonly InMemoryOverridesStore _store = new();

    private SettingsEditor Create()
    {
        var options = Options.Create(OverridesApplierTests.ValidApp());
        var effective = new EffectiveOptions(_store, options, new FakeTimeProvider(TestData.Now), NullLogger<EffectiveOptions>.Instance);
        return new SettingsEditor(_store, options, effective, new OverridesChanges(options, DefaultPrompts.Library), new FakeTopicsToFollowQueue(), NullLogger<SettingsEditor>.Instance);
    }

    [Fact]
    public async Task An_unreadable_blob_is_not_overwritten_by_ordinary_changes()
    {
        _store.SeedUnreadable();
        var editor = Create();

        var result = await editor.SetValueAsync("DigestSize", "9", CancellationToken.None);
        await editor.MarkSourceVerifiedAsync("news", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(0, _store.Saves);
        Assert.True(_store.Unreadable);
    }

    [Fact]
    public async Task Reset_all_replaces_an_unreadable_blob()
    {
        _store.SeedUnreadable();

        var result = await Create().ResetAllAsync(CancellationToken.None);

        Assert.True(result.Ok);
        Assert.False(_store.Unreadable);
    }

    [Fact]
    public async Task Reset_all_keeps_the_handled_ids_and_clears_everything_else()
    {
        _store.Seed(SettingsOverrides.Empty with
        {
            Values = new Dictionary<string, string> { ["DigestSize"] = "9" },
            Sources = [new SourceEntry("one", true)],
            AlertSources = ["one"],
            HandledUpdateIds = [7L],
        });

        await Create().ResetAllAsync(CancellationToken.None);

        Assert.Empty(_store.Current!.Values);
        Assert.Null(_store.Current.Sources);
        Assert.Null(_store.Current.AlertSources);
        Assert.Equal([7L], _store.Current.HandledUpdateIds);
    }

    [Fact]
    public async Task Effective_options_fall_back_to_the_app_settings_when_the_store_throws()
    {
        _store.FailLoad = true;
        var options = Options.Create(OverridesApplierTests.ValidApp(o => o.DigestSize = 4));
        var effective = new EffectiveOptions(_store, options, new FakeTimeProvider(TestData.Now), NullLogger<EffectiveOptions>.Instance);

        Assert.Equal(4, (await effective.GetAsync(CancellationToken.None)).DigestSize);
    }
}
