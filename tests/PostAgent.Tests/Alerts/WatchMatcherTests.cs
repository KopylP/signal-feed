using PostAgent.Alerts;
using PostAgent.Shared.Posts;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Alerts;

public class WatchMatcherTests
{
    private static PostEntity Text(string text, string? topic = null)
    {
        var post = Post("p");
        post.Text = text;
        post.Topic = topic;
        return post;
    }

    [Theory]
    [InlineData("Google rolled out the March core update today", "google core update", true)]
    [InlineData("UPDATE: Google CORE news", "google core update", true)]
    [InlineData("Google updated its core systems", "google core update", true)]
    [InlineData("Google rolled out a spam update", "google core update", false)]
    [InlineData("nothing here", "google", false)]
    public void Every_word_must_be_in_the_text_in_any_order_and_case(string text, string phrase, bool expected) =>
        Assert.Equal(expected, WatchMatcher.Matches(Text(text), phrase));

    [Fact]
    public void The_topic_the_model_gave_counts_too() =>
        Assert.True(WatchMatcher.Matches(Text("A long text about indexing.", topic: "Google core update"), "google core update"));

    [Theory]
    [InlineData("AI Overviews hurt clicks", true)]
    [InlineData("He said overviews are fine", false)]
    [InlineData("(AI) overviews", true)]
    [InlineData("ШІ і ai-пошук, overviews", true)]
    public void A_short_word_must_stand_alone(string text, bool expected) =>
        Assert.Equal(expected, WatchMatcher.Matches(Text(text), "ai overviews"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_phrase_matches_nothing(string phrase) => Assert.False(WatchMatcher.Matches(Text("anything"), phrase));

    [Fact]
    public void A_phrase_with_regex_characters_is_taken_literally() =>
        Assert.False(WatchMatcher.Matches(Text("c plus plus"), "c++ (a|b)"));
}
