using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class LineDiffTests
{
    [Fact]
    public void A_changed_line_is_shown_as_removed_and_added_and_the_rest_is_left_out()
    {
        var changes = LineDiff.Compare("one\ntwo\nthree", "one\n2\nthree");

        Assert.Equal([(LineDiff.Mark.Removed, "two"), (LineDiff.Mark.Added, "2")], changes);
    }

    [Fact]
    public void A_run_of_unchanged_lines_between_two_changes_is_one_gap()
    {
        var changes = LineDiff.Compare("a\nsame\nsame\nc", "A\nsame\nsame\nC");

        Assert.Equal(
            [(LineDiff.Mark.Removed, "a"), (LineDiff.Mark.Added, "A"), (LineDiff.Mark.Gap, ""), (LineDiff.Mark.Removed, "c"), (LineDiff.Mark.Added, "C")],
            changes);
    }

    [Fact]
    public void A_cut_shows_only_the_removed_lines()
    {
        var changes = LineDiff.Compare("keep\ndrop\nkeep too", "keep\nkeep too");

        Assert.Equal([(LineDiff.Mark.Removed, "drop")], changes);
    }

    [Fact]
    public void Equal_texts_have_no_changes()
    {
        Assert.Empty(LineDiff.Compare("a\nb", "a\nb"));
    }

    [Fact]
    public void Texts_too_big_to_align_are_shown_as_fully_replaced()
    {
        var before = string.Join('\n', Enumerable.Range(0, 1200).Select(i => $"old {i}"));
        var after = string.Join('\n', Enumerable.Range(0, 1200).Select(i => $"new {i}"));

        var changes = LineDiff.Compare(before, after);

        Assert.Equal(2400, changes.Count);
        Assert.Equal(1200, changes.Count(c => c.Mark == LineDiff.Mark.Removed));
    }
}
