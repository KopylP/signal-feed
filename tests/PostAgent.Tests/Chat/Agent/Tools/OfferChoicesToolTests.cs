using PostAgent.Chat.Agent.Tools;
using PostAgent.Shared.Settings;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat.Agent.Tools;

public class OfferChoicesToolTests
{
    private readonly MenuHarness _h = new();
    private readonly List<SettingChange> _draft = [];

    private (ToolShim Tools, ChatTurn Turn) Create()
    {
        var turn = _h.Turn(_draft, TestData.ChatId);
        return (new ToolShim(_h.ToolsFactory.Create(turn)), turn);
    }

    [Fact]
    public async Task The_options_are_kept_for_the_answer_and_nothing_is_planned_or_stored()
    {
        var (tools, turn) = Create();

        var answer = await tools.OfferChoices(["@one", "@two"], CancellationToken.None);

        Assert.StartsWith("Shown 2 buttons", answer);
        Assert.Equal(["@one", "@two"], turn.Choices);
        Assert.Empty(_draft);
        Assert.Null(_h.Store.Current);
    }

    [Fact]
    public async Task A_later_call_replaces_the_earlier_options()
    {
        var (tools, turn) = Create();

        await tools.OfferChoices(["@one"], CancellationToken.None);
        await tools.OfferChoices(["@two"], CancellationToken.None);

        Assert.Equal(["@two"], turn.Choices);
    }

    [Fact]
    public async Task Without_usable_options_no_buttons_are_shown()
    {
        var (tools, turn) = Create();

        var answer = await tools.OfferChoices(["  ", "/digest"], CancellationToken.None);

        Assert.StartsWith("No buttons shown", answer);
        Assert.Null(turn.Choices);
    }

    [Fact]
    public void Options_are_cleaned_and_a_long_one_is_left_out_not_shortened()
    {
        var clean = OfferChoicesTool.Clean([" @one ", "two\n words", "@ONE", null, "", "/start", new string('a', OfferChoicesTool.MaxOptionChars + 1)]);

        Assert.Equal(["@one", "two words"], clean);
    }

    [Fact]
    public void The_number_of_options_is_capped()
    {
        var clean = OfferChoicesTool.Clean(Enumerable.Range(1, OfferChoicesTool.MaxOptions + 5).Select(i => "o" + i));

        Assert.Equal(OfferChoicesTool.MaxOptions, clean.Count);
    }
}
