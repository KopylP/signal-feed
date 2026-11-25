using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Settings;

public class EffectiveOptionsTests
{
    private readonly InMemoryOverridesStore _store = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private EffectiveOptions Create(AgentOptions? app = null) =>
        new(_store, Options.Create(app ?? OverridesApplierTests.ValidApp()), _time, NullLogger<EffectiveOptions>.Instance);

    private static SettingsOverrides Digest(string size) =>
        SettingsOverrides.Empty with { Values = new Dictionary<string, string> { ["DigestSize"] = size } };

    [Fact]
    public async Task Returns_the_app_settings_when_there_is_no_blob()
    {
        var options = await Create(OverridesApplierTests.ValidApp(o => o.DigestSize = 4)).GetAsync(CancellationToken.None);

        Assert.Equal(4, options.DigestSize);
    }

    [Fact]
    public async Task Applies_overrides_from_the_blob()
    {
        _store.Seed(Digest("9"));

        Assert.Equal(9, (await Create().GetAsync(CancellationToken.None)).DigestSize);
    }

    [Fact]
    public async Task Caches_for_thirty_seconds_and_reloads_after()
    {
        _store.Seed(Digest("9"));
        var effective = Create();
        await effective.GetAsync(CancellationToken.None);

        _store.Seed(Digest("10"));
        _time.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(9, (await effective.GetAsync(CancellationToken.None)).DigestSize);

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(10, (await effective.GetAsync(CancellationToken.None)).DigestSize);
    }

    [Fact]
    public async Task GetFresh_ignores_the_cache()
    {
        _store.Seed(Digest("9"));
        var effective = Create();
        await effective.GetAsync(CancellationToken.None);

        _store.Seed(Digest("10"));

        Assert.Equal(10, (await effective.GetFreshAsync(CancellationToken.None)).DigestSize);
    }

    [Fact]
    public async Task Invalidate_drops_the_cache()
    {
        _store.Seed(Digest("9"));
        var effective = Create();
        await effective.GetAsync(CancellationToken.None);

        _store.Seed(Digest("10"));
        effective.Invalidate();

        Assert.Equal(10, (await effective.GetAsync(CancellationToken.None)).DigestSize);
    }

    [Fact]
    public async Task Falls_back_to_the_app_settings_when_the_overrides_make_the_options_invalid()
    {
        _store.Seed(Digest("99"));

        Assert.Equal(7, (await Create().GetAsync(CancellationToken.None)).DigestSize);
    }
}
