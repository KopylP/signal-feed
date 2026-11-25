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
        return new SettingsEditor(_store, options, effective, NullLogger<SettingsEditor>.Instance);
    }

    [Fact]
    public async Task Records_a_handled_id_and_refuses_the_same_id_again()
    {
        var editor = Create();

        Assert.True(await editor.TryMarkHandledAsync(10, CancellationToken.None));
        Assert.False(await editor.TryMarkHandledAsync(10, CancellationToken.None));
        Assert.Equal([10L], _store.Current?.HandledUpdateIds);
    }

    [Fact]
    public async Task A_lower_id_after_a_higher_one_is_still_recorded()
    {
        var editor = Create();
        await editor.TryMarkHandledAsync(101, CancellationToken.None);

        Assert.True(await editor.TryMarkHandledAsync(100, CancellationToken.None));
    }

    [Fact]
    public async Task Keeps_only_the_last_thirty_ids()
    {
        var editor = Create();

        for (long id = 1; id <= 35; id++)
        {
            await editor.TryMarkHandledAsync(id, CancellationToken.None);
        }

        Assert.Equal(30, _store.Current?.HandledUpdateIds.Count);
        Assert.DoesNotContain(5L, _store.Current!.HandledUpdateIds);
        Assert.Contains(35L, _store.Current.HandledUpdateIds);
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
