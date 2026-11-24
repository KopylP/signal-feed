using static PostAgent.Tests.TestData;
using PostAgent.Digest;

namespace PostAgent.Tests.Digest;

public class DigestReportTests
{
    private static readonly DigestItem[] Items =
    [
        new(Post("src_1"), IsInteresting: true),
        new(Post("src_2"), IsInteresting: false),
    ];

    [Fact]
    public void Insights_are_attached_to_posts_by_id()
    {
        var report = DigestReport.From(Items, new DigestSummary("Головне сьогодні",
            [new("src_1", "Перший факт"), new("src_2", "Другий факт")]));

        Assert.Equal("Головне сьогодні", report.Overview);
        Assert.Equal(["Перший факт", "Другий факт"], report.Items.Select(i => i.Insight));
    }

    [Fact]
    public void Post_without_an_insight_keeps_working_and_unknown_ids_are_ignored()
    {
        var report = DigestReport.From(Items, new DigestSummary("О", [new("src_1", "Факт"), new("вигаданий", "Зайве")]));

        Assert.Equal("Факт", report.Items[0].Insight);
        Assert.Null(report.Items[1].Insight);
    }

    [Fact]
    public void Blank_overview_and_insight_become_null()
    {
        var report = DigestReport.From(Items, new DigestSummary("  ", [new("src_1", "")]));

        Assert.Null(report.Overview);
        Assert.Null(report.Items[0].Insight);
    }

    [Fact]
    public void Links_and_other_post_data_stay_untouched()
    {
        var report = DigestReport.From(Items, new DigestSummary("О", [new("src_1", "Факт")]));

        Assert.Equal(Items[0].Post.Url, report.Items[0].Post.Url);
        Assert.Equal(Items[0].IsInteresting, report.Items[0].IsInteresting);
    }

    [Fact]
    public void Plain_report_has_no_overview()
    {
        var report = DigestReport.Plain(Items);

        Assert.Null(report.Overview);
        Assert.Equal(2, report.Items.Count);
    }
}
