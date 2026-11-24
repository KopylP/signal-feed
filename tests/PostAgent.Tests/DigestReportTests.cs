using System.Text.Json;
using PostAgent.Services;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests;

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
        var report = DigestReport.Parse(Items, """
            {"overview":"Головне сьогодні","items":[
              {"id":"src_1","insight":"Перший факт"},
              {"id":"src_2","insight":"Другий факт"}]}
            """);

        Assert.Equal("Головне сьогодні", report.Overview);
        Assert.Equal(["Перший факт", "Другий факт"], report.Items.Select(i => i.Insight));
    }

    [Fact]
    public void Post_without_an_insight_keeps_working_and_unknown_ids_are_ignored()
    {
        var report = DigestReport.Parse(Items, """
            {"overview":"О","items":[{"id":"src_1","insight":"Факт"},{"id":"вигаданий","insight":"Зайве"}]}
            """);

        Assert.Equal("Факт", report.Items[0].Insight);
        Assert.Null(report.Items[1].Insight);
    }

    [Fact]
    public void Blank_overview_and_insight_become_null()
    {
        var report = DigestReport.Parse(Items, """{"overview":"  ","items":[{"id":"src_1","insight":""}]}""");

        Assert.Null(report.Overview);
        Assert.Null(report.Items[0].Insight);
    }

    [Fact]
    public void Links_and_other_post_data_stay_untouched()
    {
        var report = DigestReport.Parse(Items, """{"overview":"О","items":[{"id":"src_1","insight":"Факт"}]}""");

        Assert.Equal(Items[0].Post.Url, report.Items[0].Post.Url);
        Assert.Equal(Items[0].IsInteresting, report.Items[0].IsInteresting);
    }

    [Fact]
    public void Malformed_response_throws_so_the_digest_falls_back_to_the_plain_version()
    {
        Assert.ThrowsAny<JsonException>(() => DigestReport.Parse(Items, "not json"));
    }

    [Fact]
    public void Plain_report_has_no_overview()
    {
        var report = DigestReport.Plain(Items);

        Assert.Null(report.Overview);
        Assert.Equal(2, report.Items.Count);
    }
}
