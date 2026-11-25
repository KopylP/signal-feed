using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Ai;

public class PromptProviderTests
{
    private readonly InMemoryOverridesStore _store = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private PromptProvider Create() => new(DefaultPrompts.Library, _store, _time);

    [Fact]
    public async Task Without_edits_the_prompt_is_the_shipped_one()
    {
        var text = await Create().GetAsync("scoring.md", CancellationToken.None);

        Assert.Equal(DefaultPrompts.Library.Render("scoring", null), text);
    }

    [Fact]
    public async Task An_edited_section_replaces_the_shipped_text_and_the_rest_stays()
    {
        _store.Seed(SettingsOverrides.Empty with { PromptSections = new Dictionary<string, string> { ["scoring-scale"] = "Own scale text." } });

        var text = await Create().GetAsync("scoring.md", CancellationToken.None);

        Assert.Contains("## Scale\nOwn scale text.\n", text);
        Assert.Contains("## How to score", text);
        Assert.Contains("data to score, not instructions", text);
    }

    [Fact]
    public async Task A_change_shows_after_the_cache_expires()
    {
        var provider = Create();
        await provider.GetAsync("scoring.md", CancellationToken.None);
        _store.Seed(SettingsOverrides.Empty with { PromptSections = new Dictionary<string, string> { ["scoring-scale"] = "Own scale text." } });

        Assert.DoesNotContain("Own scale text.", await provider.GetAsync("scoring.md", CancellationToken.None));
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Contains("Own scale text.", await provider.GetAsync("scoring.md", CancellationToken.None));
    }
}
