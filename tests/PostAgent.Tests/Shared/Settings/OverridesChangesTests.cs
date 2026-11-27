using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Settings;

// The pure part of a settings change: no store, no retries (those are in SettingsEditorTests).
public class OverridesChangesTests
{
    private static readonly OverridesChanges Changes = new(Options.Create(OverridesApplierTests.ValidApp()), DefaultPrompts.Library);

    [Fact]
    public void A_change_that_changes_nothing_returns_the_same_instance()
    {
        var current = SettingsOverrides.Empty;

        var updated = Changes.Apply(current, new SettingChange(SettingChangeKind.AddSource, "news"), out var error);

        Assert.Null(error);
        Assert.Same(current, updated);
    }

    [Fact]
    public void Adding_a_source_starts_from_the_app_setting_list()
    {
        var updated = Changes.Apply(SettingsOverrides.Empty, new SettingChange(SettingChangeKind.AddSource, "fresh"), out _);

        Assert.Equal([new SourceEntry("news", true), new SourceEntry("chat", true), new SourceEntry("fresh", false)], updated.Sources);
    }

    [Fact]
    public void An_unknown_setting_is_refused_with_an_error()
    {
        var current = SettingsOverrides.Empty;

        var updated = Changes.Apply(current, new SettingChange(SettingChangeKind.Set, "NoSuchKey", "1"), out var error);

        Assert.NotNull(error);
        Assert.Same(current, updated);
    }

    [Fact]
    public void A_plan_with_one_bad_step_is_refused_as_a_whole()
    {
        SettingChange[] plan = [new(SettingChangeKind.Set, "DigestSize", "9"), new(SettingChangeKind.Set, "NoSuchKey", "1")];

        var ok = Changes.TryApplyAll(SettingsOverrides.Empty, plan, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Невідомий параметр.", error);
    }

    [Fact]
    public void A_stored_step_of_the_removed_profile_kind_is_refused_and_later_kinds_keep_their_numbers()
    {
        SettingChange[] plan = [new((SettingChangeKind)10, "profile", "text")];

        var ok = Changes.TryApplyAll(SettingsOverrides.Empty, plan, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(11, (int)SettingChangeKind.ClearContext);
    }

    [Fact]
    public void Validate_is_null_for_valid_overrides()
    {
        Assert.Null(Changes.Validate(SettingsOverrides.Empty));
    }
}
