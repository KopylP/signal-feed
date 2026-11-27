using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.SettingsMenu;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.Shared.Settings;

namespace PostAgent.Tests.SettingsMenu;

public class MenuStateTests
{
    private readonly InMemoryOverridesStore _store = new();

    private MenuState Create()
    {
        var options = Options.Create(OverridesApplierTests.ValidApp());
        var effective = new EffectiveOptions(_store, options, new FakeTimeProvider(TestData.Now), NullLogger<EffectiveOptions>.Instance);
        return new MenuState(new SettingsEditor(_store, options, effective, new OverridesChanges(options, DefaultPrompts.Library), NullLogger<SettingsEditor>.Instance));
    }

    [Fact]
    public async Task Records_a_handled_id_and_refuses_the_same_id_again()
    {
        var menu = Create();

        Assert.True(await menu.TryMarkHandledAsync(10, CancellationToken.None));
        Assert.False(await menu.TryMarkHandledAsync(10, CancellationToken.None));
        Assert.Equal([10L], _store.Current?.HandledUpdateIds);
    }

    [Fact]
    public async Task A_lower_id_after_a_higher_one_is_still_recorded()
    {
        var menu = Create();
        await menu.TryMarkHandledAsync(101, CancellationToken.None);

        Assert.True(await menu.TryMarkHandledAsync(100, CancellationToken.None));
    }

    [Fact]
    public async Task Keeps_only_the_last_thirty_ids()
    {
        var menu = Create();

        for (long id = 1; id <= 35; id++)
        {
            await menu.TryMarkHandledAsync(id, CancellationToken.None);
        }

        Assert.Equal(30, _store.Current?.HandledUpdateIds.Count);
        Assert.DoesNotContain(5L, _store.Current!.HandledUpdateIds);
        Assert.Contains(35L, _store.Current.HandledUpdateIds);
    }
}
