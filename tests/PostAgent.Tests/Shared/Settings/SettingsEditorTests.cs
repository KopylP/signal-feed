using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsEditorTests
{
    private readonly InMemoryOverridesStore _store = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private (SettingsEditor Editor, EffectiveOptions Effective) Create(AgentOptions? app = null)
    {
        var options = Options.Create(app ?? OverridesApplierTests.ValidApp());
        var effective = new EffectiveOptions(_store, options, _time, NullLogger<EffectiveOptions>.Instance);
        return (new SettingsEditor(_store, options, effective, DefaultPrompts.Library, new InMemoryProfileStore(), new InMemoryChatStateStore(), NullLogger<SettingsEditor>.Instance), effective);
    }

    [Fact]
    public async Task Saves_a_valid_value_and_invalidates_the_cache()
    {
        var (editor, effective) = Create();
        await effective.GetAsync(CancellationToken.None);

        var result = await editor.SetValueAsync("DigestSize", "9", CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("9", _store.Current?.Values["DigestSize"]);
        Assert.Equal(9, (await effective.GetAsync(CancellationToken.None)).DigestSize);
    }

    [Fact]
    public async Task Refuses_a_value_that_breaks_a_cross_field_rule_and_leaves_the_blob_unchanged()
    {
        var (editor, _) = Create(OverridesApplierTests.ValidApp(o => o.AlertWindowHours = 20));

        var result = await editor.SetValueAsync("AlertCooldownHours", "10", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("AlertWindowHours", result.Error);
        Assert.Contains("AlertCooldownHours", result.Error);
        Assert.Null(_store.Current);
    }

    [Theory]
    [InlineData("DigestSize", "many")]
    [InlineData("DigestSize", "13")]
    [InlineData("ChatReasoningEffort", "extreme")]
    [InlineData("OpenAIModel", "gpt-unknown")]
    [InlineData("OpenAIApiKey", "stolen")]
    [InlineData("AlertQuietHours", "23-07")]
    public async Task Refuses_a_value_the_catalog_does_not_allow(string key, string value)
    {
        var (editor, _) = Create();

        var result = await editor.SetValueAsync(key, value, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Null(_store.Current);
    }

    [Fact]
    public async Task Reset_is_allowed_even_when_the_app_settings_are_invalid()
    {
        _store.Seed(SettingsOverrides.Empty with { Values = new Dictionary<string, string> { ["DigestSize"] = "5" } });
        var (editor, _) = Create(OverridesApplierTests.ValidApp(o => o.DigestSize = 99));

        var result = await editor.ResetAsync("DigestSize", CancellationToken.None);

        Assert.True(result.Ok);
        Assert.False(_store.Current?.Values.ContainsKey("DigestSize"));
    }

    [Fact]
    public async Task Resets_every_parameter_of_a_group()
    {
        _store.Seed(SettingsOverrides.Empty with
        {
            Values = new Dictionary<string, string> { ["DigestSize"] = "5", ["InterestingThreshold"] = "6", ["AlertThreshold"] = "8" },
        });
        var (editor, _) = Create();

        await editor.ResetGroupAsync(SettingGroup.Digest, CancellationToken.None);

        Assert.Equal(["AlertThreshold"], _store.Current?.Values.Keys);
    }

    [Fact]
    public async Task Retries_after_an_etag_conflict()
    {
        var (editor, _) = Create();
        var conflicts = 1;
        _store.BeforeSave = () =>
        {
            if (conflicts-- > 0)
            {
                _store.Seed(SettingsOverrides.Empty with { HandledUpdateIds = [5L] });
            }
        };

        var result = await editor.SetValueAsync("DigestSize", "9", CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("9", _store.Current?.Values["DigestSize"]);
        Assert.Equal([5L], _store.Current?.HandledUpdateIds);
    }

    [Fact]
    public async Task Gives_up_after_three_conflicts_with_an_error_result()
    {
        var (editor, _) = Create();
        _store.BeforeSave = () => _store.Seed(SettingsOverrides.Empty);

        var result = await editor.SetValueAsync("DigestSize", "9", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Marks_a_source_verified()
    {
        _store.Seed(SettingsOverrides.Empty with { Sources = [new SourceEntry("news", true), new SourceEntry("fresh", false)] });
        var (editor, _) = Create();

        await editor.MarkSourceVerifiedAsync("fresh", CancellationToken.None);

        Assert.All(_store.Current?.Sources!, s => Assert.True(s.Verified));
    }

    [Fact]
    public async Task Marking_does_not_write_when_the_sources_are_not_overridden()
    {
        var (editor, _) = Create();

        await editor.MarkSourceVerifiedAsync("news", CancellationToken.None);

        Assert.Equal(0, _store.Saves);
    }
}
