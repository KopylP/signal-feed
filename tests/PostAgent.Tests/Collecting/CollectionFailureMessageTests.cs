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
    public void Shows_only_the_first_failures_and_counts_the_rest()
    {
        var failures = Enumerable.Range(1, 15).Select(i => $"@src{i}: error").ToList();

        var html = CollectionFailureMessage.Format(failures);

        Assert.Contains("@src10: error", html);
        Assert.DoesNotContain("@src11: error", html);
        Assert.Contains("…і ще 5", html);
    }
}
