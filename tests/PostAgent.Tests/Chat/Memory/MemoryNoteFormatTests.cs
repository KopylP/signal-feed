using PostAgent.Chat.Memory;

namespace PostAgent.Tests.Chat.Memory;

public class MemoryNoteFormatTests
{
    private static readonly DateOnly Day = new(2026, 11, 27);

    private static MemoryNote Build(string title, string[] keywords, string[] facts)
    {
        Assert.True(MemoryNoteFormat.TryBuild(title, keywords, facts, Day, out var note, out var error), error);
        return note!;
    }

    private static string? Error(string title, string[] keywords, string[] facts)
    {
        Assert.False(MemoryNoteFormat.TryBuild(title, keywords, facts, Day, out _, out var error));
        return error;
    }

    [Fact]
    public void A_note_is_a_structured_markdown_file_named_by_its_day_and_keywords()
    {
        var note = Build("Робота і цілі", ["Work", "SEO"], ["Веде маркетплейс", "## Цілі", "Більше трафіку"]);

        Assert.Equal("2026-11-27-work-seo.md", note.FileName);
        Assert.Equal("# Робота і цілі\nKeywords: Work, SEO\nUpdated: 2026-11-27\n\n- Веде маркетплейс\n## Цілі\n- Більше трафіку\n", note.Text);
        Assert.Equal("Робота і цілі", note.Title);
    }

    [Fact]
    public void Keywords_in_any_script_become_a_lower_case_slug()
    {
        Assert.Equal("2026-11-27-маркетинг-google-core-update.md", Build("T", ["Маркетинг", "Google Core/Update!"], ["x"]).FileName);
    }

    [Fact]
    public void The_model_s_own_bullets_and_line_breaks_are_normalized()
    {
        var note = Build("T", ["k"], ["- one", "* two\n• three", "  ", "#"]);

        Assert.EndsWith("\n\n- one\n- two\n- three\n", note.Text);
    }

    [Fact]
    public void A_note_longer_than_fifty_lines_is_refused()
    {
        var facts = Enumerable.Range(0, MemoryNoteFormat.MaxFactLines + 1).Select(i => $"fact {i}").ToArray();

        Assert.StartsWith("Too many lines", Error("T", ["k"], facts));
        Assert.True(MemoryNoteFormat.TryBuild("T", ["k"], facts[..^1], Day, out var note, out _));
        Assert.Equal(MemoryNoteFormat.MaxLines, note!.Text.TrimEnd('\n').Split('\n').Length);
    }

    [Theory]
    [InlineData("", "k", "fact")]
    [InlineData("T", "", "fact")]
    [InlineData("T", "k", "")]
    [InlineData("T", "!!!", "fact")]
    public void A_note_without_a_title_keywords_or_facts_is_refused(string title, string keyword, string fact)
    {
        Assert.NotNull(Error(title, [keyword], [fact]));
    }

    [Fact]
    public void Too_many_keywords_and_too_long_lines_are_refused()
    {
        Assert.NotNull(Error("T", ["a", "b", "c", "d", "e", "f"], ["x"]));
        Assert.NotNull(Error("T", ["k"], [new string('x', MemoryNoteFormat.MaxLineChars + 1)]));
        Assert.NotNull(Error(new string('x', MemoryNoteFormat.MaxTitleChars + 1), ["k"], ["x"]));
    }

    [Theory]
    [InlineData("2026-11-27-work.md", true)]
    [InlineData("2026-11-27-WORK", true)]
    [InlineData(" 2026-11-27-work.md ", true)]
    [InlineData("2026-11-27-wor", false)]
    [InlineData(null, false)]
    public void A_note_is_found_by_its_name_with_or_without_the_extension(string? asked, bool same)
    {
        Assert.Equal(same, MemoryNoteFormat.SameName("2026-11-27-work.md", asked));
    }
}
