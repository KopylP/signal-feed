using PostAgent.SettingsMenu;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Tests.SettingsMenu;

public class SettingsInboxTests
{
    [Fact]
    public async Task Opens_the_menu_on_settings_command()
    {
        var h = new MenuHarness();

        var handled = await h.Inbox.HandleAsync(MenuHarness.Text("/settings"), CancellationToken.None);

        Assert.True(handled);
        Assert.Single(h.Panel.Sent);
    }

    [Fact]
    public async Task Opens_the_menu_on_settings_command_addressed_to_the_bot()
    {
        var h = new MenuHarness();

        Assert.True(await h.Inbox.HandleAsync(MenuHarness.Text("/settings@signal_bot"), CancellationToken.None));
        Assert.Single(h.Panel.Sent);
    }

    [Fact]
    public async Task Ignores_a_callback_from_another_chat()
    {
        var h = new MenuHarness();

        var handled = await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", chatId: 99), CancellationToken.None);

        Assert.True(handled);
        Assert.Null(h.Store.Current);
        Assert.Empty(h.Panel.Edited);
        Assert.Empty(h.Panel.Answered);
    }

    [Fact]
    public async Task Ignores_a_group_chat()
    {
        var h = new MenuHarness();

        await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", chatId: MenuHarness.BoundChat, type: ChatType.Group), CancellationToken.None);
        await h.Inbox.HandleAsync(MenuHarness.Text("/settings", type: ChatType.Group), CancellationToken.None);

        Assert.Null(h.Store.Current);
        Assert.Empty(h.Panel.Sent);
    }

    [Fact]
    public async Task Does_nothing_while_the_agent_is_not_paired()
    {
        var h = new MenuHarness(bound: null);

        Assert.False(await h.Inbox.HandleAsync(MenuHarness.Text("/settings"), CancellationToken.None));
        Assert.Empty(h.Panel.Sent);
    }

    [Fact]
    public async Task Answers_every_callback_query_even_with_data_it_does_not_know()
    {
        var h = new MenuHarness();

        await h.Inbox.HandleAsync(MenuHarness.Callback("garbage", updateId: 1), CancellationToken.None);
        await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", updateId: 2), CancellationToken.None);

        Assert.Equal(["cb1", "cb2"], h.Panel.Answered.Select(a => a.Id));
    }

    [Fact]
    public async Task A_button_press_changes_the_setting()
    {
        var h = new MenuHarness();

        await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9"), CancellationToken.None);

        Assert.Equal("9", h.Store.Current?.Values["DigestSize"]);
    }

    [Fact]
    public async Task A_lower_update_id_after_a_higher_one_is_still_handled()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", updateId: 101), CancellationToken.None);

        await h.Inbox.HandleAsync(MenuHarness.Callback("n:InterestingThreshold:6", updateId: 100), CancellationToken.None);

        Assert.Equal("6", h.Store.Current?.Values["InterestingThreshold"]);
    }

    [Fact]
    public async Task A_chat_question_with_a_lower_id_than_the_last_menu_update_still_reaches_the_chat()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", updateId: 9000), CancellationToken.None);

        Assert.False(await h.Inbox.HandleAsync(MenuHarness.Text("питання про дайджест", updateId: 12), CancellationToken.None));
    }

    [Fact]
    public async Task An_expired_pending_question_is_cleared_and_the_text_goes_to_the_chat()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("t:AlertQuietHours", updateId: 1), CancellationToken.None);
        h.Time.Advance(TimeSpan.FromMinutes(11));
        var saves = h.Store.Saves;

        var handled = await h.Inbox.HandleAsync(MenuHarness.Text("не відповідь, а питання", updateId: 2), CancellationToken.None);

        Assert.False(handled);
        Assert.Null(h.Store.Current?.Pending);
        Assert.Equal(saves + 1, h.Store.Saves);
    }

    [Fact]
    public async Task The_menu_still_opens_when_the_blob_is_unreadable_and_offers_a_reset()
    {
        var h = new MenuHarness();
        h.Store.SeedUnreadable();

        var handled = await h.Inbox.HandleAsync(MenuHarness.Text("/settings"), CancellationToken.None);

        Assert.True(handled);
        var sent = Assert.Single(h.Panel.Sent);
        Assert.Contains("ra", MenuHarness.Buttons(sent.Keyboard));
        Assert.Contains("прочитати", sent.Html);
        Assert.True(h.Store.Unreadable);
    }

    [Fact]
    public async Task A_failing_blob_read_leaves_the_update_to_the_chat()
    {
        var h = new MenuHarness();
        h.Store.FailLoad = true;

        Assert.False(await h.Inbox.HandleAsync(MenuHarness.Text("питання"), CancellationToken.None));
    }

    [Fact]
    public async Task Ignores_a_replayed_update_id()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", updateId: 5), CancellationToken.None);
        var edits = h.Panel.Edited.Count;
        var saves = h.Store.Saves;

        var handled = await h.Inbox.HandleAsync(MenuHarness.Callback("n:DigestSize:9", updateId: 5), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(edits, h.Panel.Edited.Count);
        Assert.Equal(saves, h.Store.Saves);
    }

    [Fact]
    public async Task A_resent_answer_is_not_saved_twice_and_does_not_reach_the_chat_as_a_question()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("t:AlertQuietHours", updateId: 1), CancellationToken.None);
        var answer = MenuHarness.Text("23:00-07:00", updateId: 2);

        var first = await h.Inbox.HandleAsync(answer, CancellationToken.None);
        var saves = h.Store.Saves;
        var replay = await h.Inbox.HandleAsync(answer, CancellationToken.None);

        Assert.True(first);
        Assert.True(replay);
        Assert.Equal(saves, h.Store.Saves);
    }

    [Fact]
    public async Task Text_without_a_pending_question_goes_to_the_chat()
    {
        var h = new MenuHarness();

        Assert.False(await h.Inbox.HandleAsync(MenuHarness.Text("розкажи про пункт 3"), CancellationToken.None));
        Assert.Null(h.Store.Current);
    }

    [Fact]
    public async Task Another_command_cancels_pending_and_is_left_to_others()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("t:AlertQuietHours", updateId: 1), CancellationToken.None);

        var handled = await h.Inbox.HandleAsync(MenuHarness.Text("/help", updateId: 2), CancellationToken.None);

        Assert.False(handled);
        Assert.Null(h.Store.Current?.Pending);
    }

    [Fact]
    public async Task The_cancel_command_clears_pending()
    {
        var h = new MenuHarness();
        await h.Inbox.HandleAsync(MenuHarness.Callback("t:AlertQuietHours", updateId: 1), CancellationToken.None);

        Assert.True(await h.Inbox.HandleAsync(MenuHarness.Text("/cancel", updateId: 2), CancellationToken.None));

        Assert.Null(h.Store.Current?.Pending);
    }

    [Fact]
    public async Task A_second_bound_chat_gets_its_own_menu()
    {
        var h = new MenuHarness();
        h.AlsoBind(43);

        Assert.True(await h.Inbox.HandleAsync(MenuHarness.Text("/settings", chatId: 43), CancellationToken.None));

        Assert.Equal(43, Assert.Single(h.Panel.Sent).ChatId);
    }

    [Fact]
    public async Task The_typed_answer_of_one_chat_is_not_taken_for_the_answer_to_the_question_of_another()
    {
        var h = new MenuHarness();
        h.AlsoBind(43);
        await h.Inbox.HandleAsync(MenuHarness.Callback(CallbackData.Encode(MenuAction.AskText("AlertQuietHours")), chatId: 42), CancellationToken.None);

        var handled = await h.Inbox.HandleAsync(MenuHarness.Text("23:00-07:00", updateId: 2, chatId: 43), CancellationToken.None);

        Assert.False(handled);
        Assert.NotNull(h.Store.Current?.Pending);
    }

    [Fact]
    public async Task Opening_the_menu_in_one_chat_keeps_the_question_asked_in_another()
    {
        var h = new MenuHarness();
        h.AlsoBind(43);
        await h.Inbox.HandleAsync(MenuHarness.Callback(CallbackData.Encode(MenuAction.AskText("AlertQuietHours")), chatId: 42), CancellationToken.None);

        await h.Inbox.HandleAsync(MenuHarness.Text("/settings", updateId: 2, chatId: 43), CancellationToken.None);

        Assert.Equal(42, h.Store.Current?.Pending?.ChatId);
    }
}
