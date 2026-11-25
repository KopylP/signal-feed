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
}
