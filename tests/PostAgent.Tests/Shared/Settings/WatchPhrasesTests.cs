using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class WatchPhrasesTests
{
    [Theory]
    [InlineData("google core update", "google core update")]
    [InlineData("  Google   CORE\tupdate  ", "Google CORE update")]
    [InlineData("a;b", "a b")]
    [InlineData("<b>x</b>", "b x b")]
    [InlineData("line\nbreak", "line break")]
    public void A_phrase_is_one_clean_line(string text, string expected) =>
        Assert.Equal(expected, WatchPhrases.Normalize(text));

    [Theory]
    [InlineData("\"google core update\"", "google core update")]
    [InlineData("google, core update?", "google core update")]
    [InlineData("(march) core-update!", "march core-update")]
    [InlineData("C# and c++", "C# and c++")]
    [InlineData("«оновлення google»", "оновлення google")]
    [InlineData("go\u200bogle core\u00ADupdate \u202Eevil", "google coreupdate evil")]
    public void Marks_around_a_word_and_invisible_characters_are_dropped_but_not_inside_a_word(string text, string expected) =>
        Assert.Equal(expected, WatchPhrases.Normalize(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(";;;")]
    public void Nothing_left_is_no_phrase(string? text) => Assert.Null(WatchPhrases.Normalize(text));

    [Fact]
    public void A_phrase_longer_than_the_limit_is_refused() =>
        Assert.Null(WatchPhrases.Normalize(new string('a', WatchPhrases.MaxChars + 1)));

    [Fact]
    public void The_setting_is_split_cleaned_deduplicated_and_capped()
    {
        var setting = "google core update; AI overviews ;google core update;;" + string.Join(';', Enumerable.Range(1, 40).Select(i => "topic" + i));

        var phrases = WatchPhrases.Parse(setting);

        Assert.Equal(WatchPhrases.MaxCount, phrases.Count);
        Assert.Equal(["google core update", "AI overviews", "topic1"], phrases.Take(3));
    }

    [Fact]
    public void Joining_and_parsing_give_the_same_list()
    {
        var phrases = new[] { "google core update", "ai overviews" };

        Assert.Equal(phrases, WatchPhrases.Parse(WatchPhrases.Join(phrases)));
    }
}
