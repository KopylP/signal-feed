using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Settings;

// The fake carries the ETag contract that the editor and the menu tests rely on, so the contract is pinned here.
public class InMemoryOverridesStoreTests
{
    private static SettingsOverrides Marked(string value) =>
        SettingsOverrides.Empty with { Values = new Dictionary<string, string> { ["DigestSize"] = value } };

    [Fact]
    public async Task Saves_when_the_etag_matches()
    {
        var store = new InMemoryOverridesStore();
        store.Seed(Marked("5"));
        var handle = await store.LoadAsync(CancellationToken.None);

        Assert.True(await store.TrySaveAsync(handle, Marked("6"), CancellationToken.None));
        Assert.Equal("6", store.Current?.Values["DigestSize"]);
    }

    [Fact]
    public async Task Refuses_a_save_with_a_stale_etag()
    {
        var store = new InMemoryOverridesStore();
        store.Seed(Marked("5"));
        var handle = await store.LoadAsync(CancellationToken.None);
        store.Seed(Marked("7"));

        Assert.False(await store.TrySaveAsync(handle, Marked("6"), CancellationToken.None));
        Assert.Equal("7", store.Current?.Values["DigestSize"]);
    }

    [Fact]
    public async Task Creates_only_when_absent()
    {
        var store = new InMemoryOverridesStore();

        Assert.True(await store.TrySaveAsync(null, Marked("5"), CancellationToken.None));
        Assert.False(await store.TrySaveAsync(null, Marked("6"), CancellationToken.None));
        Assert.Equal("5", store.Current?.Values["DigestSize"]);
    }

    [Fact]
    public async Task Load_returns_null_when_nothing_is_stored()
    {
        Assert.Null(await new InMemoryOverridesStore().LoadAsync(CancellationToken.None));
    }
}
