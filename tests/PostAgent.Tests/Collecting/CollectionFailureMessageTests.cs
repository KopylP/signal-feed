using PostAgent.Collecting;

namespace PostAgent.Tests.Collecting;

public class CollectionFailureMessageTests
{
    [Fact]
    public void Lists_failures_escaped()
    {
        var html = CollectionFailureMessage.Format(["@news: <boom> & more", "@chat: timeout"]);

        Assert.StartsWith("⚠️", html);
        Assert.Contains("@news: &lt;boom&gt; &amp; more", html);
        Assert.Contains("@chat: timeout", html);
    }

    [Fact]
    public void Offers_a_removal_button_per_source_with_the_callback_data_of_the_menu()
    {
        var keyboard = CollectionFailureMessage.RemovalKeyboard(["fresh_one", "fresh_two"]);

        var buttons = keyboard.Rows.SelectMany(r => r).ToList();
        Assert.Equal(["sd:fresh_one", "sd:fresh_two"], buttons.Select(b => b.Data));
        Assert.Contains("@fresh_one", buttons[0].Text);
    }

    [Fact]
    public void No_sources_means_no_keyboard()
    {
        Assert.Empty(CollectionFailureMessage.RemovalKeyboard([]).Rows);
    }

    [Fact]
    public void Shows_only_the_first_failures_and_counts_the_rest()
    {
        var failures = Enumerable.Range(1, 15).Select(i => $"@src{i}: error").ToList();

        var html = CollectionFailureMessage.Format(failures);

        Assert.Contains("@src10: error", html);
        Assert.DoesNotContain("@src11: error", html);
        Assert.Contains("…і ще 5", html);
    }
}
