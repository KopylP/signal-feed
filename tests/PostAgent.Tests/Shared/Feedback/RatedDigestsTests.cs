using PostAgent.Shared.Chat;
using PostAgent.Shared.Feedback;

namespace PostAgent.Tests.Shared.Feedback;

public class RatedDigestsTests
{
    private static ReportSnapshot Report(string id) => new(id, TestData.Now, null,
        [new SnapshotEntry("Headlines", "2026-11-24/a_1", "chan", TestData.Now, "Тема", "Суть", null, "Довгий уривок", 9, "https://t.me/chan/1")], []);

    [Fact]
    public void Only_the_latest_reports_are_kept()
    {
        var kept = Enumerable.Range(1, RatedDigests.MaxKept + 2)
            .Aggregate((IReadOnlyList<ReportSnapshot>)[], (list, n) => RatedDigests.Add(list, Report($"d{n}")));

        Assert.Equal(RatedDigests.MaxKept, kept.Count);
        Assert.Equal($"d{RatedDigests.MaxKept + 2}", kept[^1].ReportId);
        Assert.DoesNotContain(kept, r => r.ReportId == "d1");
    }

    [Fact]
    public void A_kept_report_survives_writing_and_reading_without_its_excerpts()
    {
        var read = RatedDigests.Parse(RatedDigests.Write(RatedDigests.Add([], Report("d1"))));

        var entry = Assert.Single(Assert.Single(read).Entries);
        Assert.Equal(("2026-11-24/a_1", "chan", "Тема", string.Empty), (entry.PostId, entry.Source, entry.Title, entry.Excerpt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void A_damaged_file_reads_as_empty(string json) => Assert.Empty(RatedDigests.Parse(json));
}
