using PostAgent.SettingsMenu;
using PostAgent.Shared.Settings;

namespace PostAgent.Tests.SettingsMenu;

public class CallbackDataTests
{
    public static TheoryData<MenuAction> Actions() =>
    [
        MenuAction.Main,
        MenuAction.OpenGroup(SettingGroup.Alerts),
        MenuAction.OpenSources,
        MenuAction.OpenChoice("OpenAIModel"),
        MenuAction.SetInt("DigestSize", 8),
        MenuAction.Choose("OpenAIModel", 2),
        MenuAction.AskText("AlertQuietHours"),
        MenuAction.Reset("DigestSize"),
        MenuAction.ResetGroup(SettingGroup.Digest),
        MenuAction.Cancel,
        MenuAction.ResetAll,
        MenuAction.AddSource,
        MenuAction.RemoveSource("some_channel"),
        MenuAction.ToggleAlert("some_channel"),
        MenuAction.ApplyProposal("123456789"),
        MenuAction.DeclineProposal("123456789"),
    ];

    [Theory]
    [MemberData(nameof(Actions))]
    public void Round_trips_every_action(MenuAction action)
    {
        Assert.Equal(action, CallbackData.Decode(CallbackData.Encode(action)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("zzz")]
    [InlineData("n:DigestSize")]
    [InlineData("n:DigestSize:abc")]
    [InlineData("g:Nope")]
    [InlineData("c:OpenAIModel:x")]
    [InlineData("sd:")]
    public void Rejects_unknown_and_malformed_data(string data)
    {
        Assert.Null(CallbackData.Decode(data));
    }

    [Fact]
    public void Every_button_of_every_screen_has_data_up_to_64_bytes()
    {
        // KeyboardButton throws above 64 bytes, so building the screens is the check.
        var everything = SettingsCatalog.All.ToDictionary(d => d.Key, _ => "1");
        var overrides = SettingsOverrides.Empty with { Values = everything };
        var options = new AgentOptions();

        foreach (var group in Enum.GetValues<SettingGroup>().Where(g => g != SettingGroup.Sources))
        {
            MenuScreens.Group(group, options, overrides);
        }

        foreach (var definition in SettingsCatalog.All)
        {
            MenuScreens.Ask(definition, null);
            if (definition.Kind == SettingKind.Choice)
            {
                MenuScreens.Choice(definition, options);
            }
        }

        MenuScreens.Home(false);
        MenuScreens.Sources([new SourceEntry(new string('a', 32), false)], [new string('a', 32)]);
    }
}
