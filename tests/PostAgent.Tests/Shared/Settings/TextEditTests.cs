using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class TextEditTests
{
    [Fact]
    public void A_fragment_is_replaced_once()
    {
        Assert.True(TextEdit.TryApply("One. Two. Three.", "Two.", "2.", out var result, out _));
        Assert.Equal("One. 2. Three.", result);
    }

    [Fact]
    public void An_empty_before_adds_at_the_end()
    {
        Assert.True(TextEdit.TryApply("One.", "", "Two.", out var result, out _));
        Assert.Equal("One.\nTwo.", result);
    }

    [Fact]
    public void An_empty_after_removes_the_fragment()
    {
        Assert.True(TextEdit.TryApply("One. Two.", "Two.", "", out var result, out _));
        Assert.Equal("One.", result);
    }

    [Fact]
    public void A_missing_or_repeated_fragment_is_refused()
    {
        Assert.False(TextEdit.TryApply("One. One.", "One.", "x", out _, out var repeated));
        Assert.Contains("кілька разів", repeated);
        Assert.False(TextEdit.TryApply("One.", "Nine.", "x", out _, out var missing));
        Assert.Contains("не знайдено", missing);
    }

    [Fact]
    public void A_snippet_or_text_over_the_limit_is_refused()
    {
        Assert.False(TextEdit.TryApply("a", "a", new string('x', TextEdit.MaxSnippet + 1), out _, out _));
        Assert.False(TextEdit.TryApply(new string('a', TextEdit.MaxText), "", "b", out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("## New heading")]
    [InlineData("text\n# heading")]
    [InlineData("a <!-- editable: x | y --> b")]
    public void A_section_keeps_its_place_in_the_prompt(string text) => Assert.NotNull(TextEdit.CheckSection(text));

    [Fact]
    public void An_ordinary_section_text_passes() => Assert.Null(TextEdit.CheckSection("Score the posts about links higher."));
}
