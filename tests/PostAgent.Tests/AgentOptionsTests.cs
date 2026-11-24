namespace PostAgent.Tests;

public class AgentOptionsTests
{
    [Fact]
    public void Sources_are_trimmed_normalized_and_deduplicated()
    {
        var options = new AgentOptions { Sources = " @News, https://t.me/chat/ ,news,, T.me/Other " };

        Assert.Equal(["news", "chat", "other"], options.SourceList);
    }

    [Fact]
    public void Empty_AlertSources_gives_an_empty_set()
    {
        Assert.Empty(new AgentOptions().AlertSourceSet);
    }
}
