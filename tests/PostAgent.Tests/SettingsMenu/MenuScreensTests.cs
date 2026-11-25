using PostAgent.SettingsMenu;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Tests.SettingsMenu;

public class MenuScreensTests
{
    private static SettingsOverrides Set(string key, string value) =>
        SettingsOverrides.Empty with { Values = new Dictionary<string, string> { [key] = value } };

    [Fact]
    public void Marks_a_value_set_from_the_bot()
    {
        var screen = MenuScreens.Group(SettingGroup.Digest, new AgentOptions { DigestSize = 9 }, Set("DigestSize", "9"));

        Assert.Equal(1, screen.Html.Split('✎').Length - 1);
    }

    [Fact]
    public void Offers_a_reset_only_for_a_value_set_from_the_bot()
    {
        var plain = MenuScreens.Group(SettingGroup.Digest, new AgentOptions(), SettingsOverrides.Empty);
        var changed = MenuScreens.Group(SettingGroup.Digest, new AgentOptions { DigestSize = 9 }, Set("DigestSize", "9"));

        Assert.DoesNotContain("r:DigestSize", MenuHarness.Buttons(plain.Keyboard));
        Assert.Contains("r:DigestSize", MenuHarness.Buttons(changed.Keyboard));
    }

    [Fact]
    public void Clamps_the_plus_button_at_the_maximum()
    {
        var screen = MenuScreens.Group(SettingGroup.Digest, new AgentOptions { DigestSize = AgentOptions.MaxDigestItems }, SettingsOverrides.Empty);

        var buttons = MenuHarness.Buttons(screen.Keyboard).ToList();

        Assert.Contains($"n:DigestSize:{AgentOptions.MaxDigestItems}", buttons);
        Assert.Contains($"n:DigestSize:{AgentOptions.MaxDigestItems - 1}", buttons);
    }

    [Fact]
    public void Marks_the_current_model()
    {
        var definition = SettingsCatalog.Find("OpenAIChatModel")!;

        var screen = MenuScreens.Choice(definition, new AgentOptions { OpenAIChatModel = "gpt-6.1-terra" });

        var marked = screen.Keyboard.Rows.SelectMany(r => r).Where(b => b.Text.Contains('✅')).ToList();
        Assert.Equal("c:OpenAIChatModel:1", Assert.Single(marked).Data);
    }

    [Fact]
    public void Shows_a_model_that_is_not_on_the_list_and_keeps_it()
    {
        var definition = SettingsCatalog.Find("OpenAIModel")!;

        var screen = MenuScreens.Choice(definition, new AgentOptions { OpenAIModel = "gpt-custom" });

        Assert.Contains("gpt-custom", screen.Html);
        Assert.DoesNotContain(screen.Keyboard.Rows.SelectMany(r => r), b => b.Text.Contains('✅'));
    }

    [Fact]
    public void The_main_screen_says_when_the_bot_values_are_ignored()
    {
        Assert.Contains("ігнор", MenuScreens.Home(overridesIgnored: true).Html);
        Assert.DoesNotContain("ігнор", MenuScreens.Home(overridesIgnored: false).Html);
    }

    [Fact]
    public void Escapes_a_text_value_in_the_message()
    {
        var screen = MenuScreens.Group(SettingGroup.Alerts, new AgentOptions { AlertTimeZone = "<b>x" }, SettingsOverrides.Empty);

        Assert.DoesNotContain("<b>x", screen.Html);
        Assert.Contains("&lt;b&gt;x", screen.Html);
    }

    [Fact]
    public void Every_screen_has_a_way_back()
    {
        var group = MenuScreens.Group(SettingGroup.Chat, new AgentOptions(), SettingsOverrides.Empty);
        var choice = MenuScreens.Choice(SettingsCatalog.Find("ChatReasoningEffort")!, new AgentOptions());

        Assert.Contains("m", MenuHarness.Buttons(group.Keyboard));
        Assert.Contains("g:Chat", MenuHarness.Buttons(choice.Keyboard));
    }

    [Fact]
    public void The_sources_screen_offers_a_reset_and_says_the_app_setting_is_ignored_when_the_list_is_set_from_the_bot()
    {
        var fromBot = MenuScreens.Sources([new SourceEntry("news_one", true)], [], fromBot: true);
        var fromApp = MenuScreens.Sources([new SourceEntry("news_one", true)], [], fromBot: false);

        Assert.Contains("rg:Sources", MenuHarness.Buttons(fromBot.Keyboard));
        Assert.Contains("App Settings", fromBot.Html);
        Assert.DoesNotContain("rg:Sources", MenuHarness.Buttons(fromApp.Keyboard));
    }

    [Fact]
    public void The_main_screen_offers_a_reset_of_everything_when_the_stored_settings_cannot_be_read()
    {
        var screen = MenuScreens.Home(overridesIgnored: true, unreadable: true);

        Assert.Contains("ra", MenuHarness.Buttons(screen.Keyboard));
        Assert.Contains("прочитати", screen.Html);
        Assert.DoesNotContain("ra", MenuHarness.Buttons(MenuScreens.Home(overridesIgnored: false).Keyboard));
    }
}
