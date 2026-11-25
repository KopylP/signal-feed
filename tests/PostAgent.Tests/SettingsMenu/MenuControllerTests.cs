using PostAgent.SettingsMenu;
using PostAgent.Shared.Settings;

namespace PostAgent.Tests.SettingsMenu;

public class MenuControllerTests
{
    private const long Chat = MenuHarness.BoundChat;
    private const int MenuMessage = 7;

    [Fact]
    public async Task Plus_sets_the_target_value_and_a_repeated_press_changes_nothing_more()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.SetInt("DigestSize", 8), CancellationToken.None);
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.SetInt("DigestSize", 8), CancellationToken.None);

        Assert.Equal("8", h.Store.Current?.Values["DigestSize"]);
        Assert.Contains("n:DigestSize:9", MenuHarness.Buttons(h.Panel.Edited[^1].Keyboard));
    }

    [Fact]
    public async Task Choosing_a_model_saves_it()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.Choose("OpenAIChatModel", 2), CancellationToken.None);

        Assert.Equal("gpt-6.1-sol", h.Store.Current?.Values["OpenAIChatModel"]);
    }

    [Fact]
    public async Task Asking_for_text_stores_a_pending_question_that_expires_in_ten_minutes()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AskText("AlertQuietHours"), CancellationToken.None);

        var pending = h.Store.Current?.Pending;
        Assert.Equal(new PendingInput("AlertQuietHours", "set", MenuMessage, TestData.Now.AddMinutes(10)), pending);
    }

    [Fact]
    public async Task Text_while_pending_is_saved_and_clears_pending()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AskText("AlertQuietHours"), CancellationToken.None);

        var handled = await h.Controller.HandleTextAsync(Chat, " 23:00-07:00 ", CancellationToken.None);

        Assert.True(handled);
        Assert.Equal("23:00-07:00", h.Store.Current?.Values["AlertQuietHours"]);
        Assert.Null(h.Store.Current?.Pending);
    }

    [Fact]
    public async Task Wrong_text_keeps_pending_and_explains()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AskText("AlertQuietHours"), CancellationToken.None);

        var handled = await h.Controller.HandleTextAsync(Chat, "23-07", CancellationToken.None);

        Assert.True(handled);
        Assert.NotNull(h.Store.Current?.Pending);
        Assert.False(h.Store.Current!.Values.ContainsKey("AlertQuietHours"));
        Assert.Contains("AlertQuietHours", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task Expired_pending_sends_the_text_on_to_the_chat()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AskText("AlertQuietHours"), CancellationToken.None);
        h.Time.Advance(TimeSpan.FromMinutes(11));

        Assert.False(await h.Controller.HandleTextAsync(Chat, "23:00-07:00", CancellationToken.None));
        Assert.False(h.Store.Current!.Values.ContainsKey("AlertQuietHours"));
    }

    [Fact]
    public async Task Text_without_a_pending_question_is_not_handled()
    {
        var h = new MenuHarness();

        Assert.False(await h.Controller.HandleTextAsync(Chat, "привіт", CancellationToken.None));
    }

    [Fact]
    public async Task A_second_ask_replaces_the_first_pending()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, 7, MenuAction.AskText("AlertQuietHours"), CancellationToken.None);

        await h.Controller.HandleCallbackAsync(Chat, 8, MenuAction.AskText("AlertTimeZone"), CancellationToken.None);

        Assert.Equal(new PendingInput("AlertTimeZone", "set", 8, TestData.Now.AddMinutes(10)), h.Store.Current?.Pending);
    }

    [Fact]
    public async Task Cancel_clears_pending_and_shows_the_main_screen()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AskText("AlertQuietHours"), CancellationToken.None);

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.Cancel, CancellationToken.None);

        Assert.Null(h.Store.Current?.Pending);
        Assert.Contains("g:Digest", MenuHarness.Buttons(h.Panel.Edited[^1].Keyboard));
    }

    [Fact]
    public async Task Sends_a_new_message_when_the_edit_fails()
    {
        var h = new MenuHarness();
        h.Panel.FailEdits = true;

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.OpenGroup(SettingGroup.Digest), CancellationToken.None);

        Assert.Single(h.Panel.Sent);
        Assert.Empty(h.Panel.Edited);
    }

    [Fact]
    public async Task Rejected_value_shows_the_field_names()
    {
        var h = new MenuHarness(o => o.AlertWindowHours = 20);

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.SetInt("AlertCooldownHours", 10), CancellationToken.None);

        Assert.Null(h.Store.Current);
        Assert.Contains("AlertWindowHours", h.Panel.Edited[^1].Html);
        Assert.Contains("AlertCooldownHours", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task Reset_returns_a_parameter_to_the_app_setting()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.SetInt("DigestSize", 9), CancellationToken.None);

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.Reset("DigestSize"), CancellationToken.None);

        Assert.False(h.Store.Current!.Values.ContainsKey("DigestSize"));
    }

    [Fact]
    public async Task Ignores_an_action_for_a_key_that_is_not_in_the_catalog()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.SetInt("OpenAIApiKey", 1), CancellationToken.None);
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.Choose("OpenAIModel", 99), CancellationToken.None);

        Assert.Null(h.Store.Current);
    }

    [Fact]
    public async Task The_settings_command_sends_the_main_screen_as_a_new_message()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCommandAsync(Chat, "/settings", CancellationToken.None);

        var sent = Assert.Single(h.Panel.Sent);
        Assert.Contains("g:Alerts", MenuHarness.Buttons(sent.Keyboard));
    }
}
