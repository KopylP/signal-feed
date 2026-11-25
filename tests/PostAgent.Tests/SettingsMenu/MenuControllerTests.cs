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

    [Fact]
    public async Task Asking_to_add_a_source_stores_a_pending_question()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AddSource, CancellationToken.None);

        Assert.Equal(new PendingInput("Sources", "add-source", MenuMessage, TestData.Now.AddMinutes(10)), h.Store.Current?.Pending);
    }

    [Fact]
    public async Task A_typed_username_is_added_as_not_verified_and_shown_in_the_sources_screen()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AddSource, CancellationToken.None);

        var handled = await h.Controller.HandleTextAsync(Chat, "https://t.me/Fresh_One", CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(new SourceEntry("fresh_one", false), h.Store.Current?.Sources?[^1]);
        Assert.Null(h.Store.Current?.Pending);
        Assert.Contains("⏳ @fresh_one", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task Something_that_is_not_a_username_keeps_the_question_open()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.AddSource, CancellationToken.None);

        await h.Controller.HandleTextAsync(Chat, "два слова", CancellationToken.None);

        Assert.NotNull(h.Store.Current?.Pending);
        Assert.Null(h.Store.Current?.Sources);
    }

    [Fact]
    public async Task Removing_the_last_source_is_refused_with_a_note()
    {
        var h = new MenuHarness(o => o.Sources = "only");

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.RemoveSource("only"), CancellationToken.None);

        Assert.Null(h.Store.Current);
        Assert.Contains("⚠️", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task Removing_and_toggling_a_source_update_the_screen()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.ToggleAlert("chat"), CancellationToken.None);
        Assert.Contains("@chat 🚨", h.Panel.Edited[^1].Html);

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.RemoveSource("news"), CancellationToken.None);
        Assert.DoesNotContain("@news", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task The_sources_screen_lists_the_app_setting_sources_as_verified()
    {
        var h = new MenuHarness();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.OpenSources, CancellationToken.None);

        Assert.Contains("✅ @news", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task Resetting_the_sources_group_hands_both_lists_back_to_the_app_settings()
    {
        var h = new MenuHarness();
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.ToggleAlert("chat"), CancellationToken.None);
        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.RemoveSource("news"), CancellationToken.None);

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.ResetGroup(SettingGroup.Sources), CancellationToken.None);

        Assert.Null(h.Store.Current?.Sources);
        Assert.Null(h.Store.Current?.AlertSources);
        Assert.Contains("✅ @news", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task Reset_all_replaces_an_unreadable_blob_and_shows_the_main_screen()
    {
        var h = new MenuHarness();
        h.Store.SeedUnreadable();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.ResetAll, CancellationToken.None);

        Assert.False(h.Store.Unreadable);
        Assert.DoesNotContain("прочитати", h.Panel.Edited[^1].Html);
    }

    [Fact]
    public async Task The_main_screen_says_when_the_stored_settings_cannot_be_read()
    {
        var h = new MenuHarness();
        h.Store.SeedUnreadable();

        await h.Controller.HandleCallbackAsync(Chat, MenuMessage, MenuAction.Main, CancellationToken.None);

        Assert.Contains("прочитати", h.Panel.Edited[^1].Html);
    }
}
