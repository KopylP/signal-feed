using PostAgent.Digest;
using PostAgent.Digest.Infrastructure;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestReportTests
{
    private static readonly DigestItem[] Items =
    [
        new(Post("src_1"), IsInteresting: true),
        new(Post("src_2"), IsInteresting: true),
        new(Post("src_3"), IsInteresting: false),
        new(Post("src_4"), IsInteresting: false),
    ];

    private static DigestSummary Summary(
        string overview = "Головне",
        List<HeadlineEntry>? headlines = null,
        List<NoteEntry>? forMarketplace = null,
        List<NotableEntry>? notable = null,
        List<PickEntry>? recommended = null,
        List<string>? needsVerification = null) =>
        new(overview, headlines ?? [], forMarketplace ?? [], notable ?? [], recommended ?? [], needsVerification ?? []);

    [Fact]
    public void Entries_are_attached_to_posts_by_id_in_the_model_order()
    {
        var report = DigestReport.From(Items, Summary(
            headlines: [new("src_2", "Заголовок 2", "Суть 2", "Чому 2"), new("src_1", "Заголовок 1", "Суть 1", "Чому 1")],
            forMarketplace: [new("src_3", "Для маркетплейсу", "Нотатка")]));

        Assert.Equal("Головне", report.Overview);
        Assert.Equal(["src_2", "src_1"], report.Headlines.Select(e => e.Post.RowKey));
        Assert.Equal("Суть 2", report.Headlines[0].Text);
        Assert.Equal("Чому 2", report.Headlines[0].Why);
        Assert.Equal("src_3", Assert.Single(report.ForMarketplace).Post.RowKey);
        Assert.Equal("Нотатка", report.ForMarketplace[0].Text);
    }

    [Fact]
    public void Unknown_ids_and_entries_without_text_are_dropped()
    {
        var report = DigestReport.From(Items, Summary(
            headlines: [new("вигаданий", "Зайве", "Суть", "Чому"), new("src_1", "Порожній", "  ", "Чому"), new("src_2", "Є", "Суть", "Чому")]));

        Assert.Equal(["src_2"], report.Headlines.Select(e => e.Post.RowKey));
    }

    [Fact]
    public void A_post_appears_once_across_the_main_sections_and_the_first_section_wins()
    {
        var report = DigestReport.From(Items, Summary(
            headlines: [new("src_1", "З", "Суть", "Чому"), new("src_1", "Повтор", "Суть", "Чому")],
            forMarketplace: [new("src_1", "Знов", "Нотатка")],
            notable: [new("src_2", "Інше", "Нотатка")]));

        Assert.Equal(["src_1"], report.Headlines.Select(e => e.Post.RowKey));
        Assert.Empty(report.ForMarketplace);
        Assert.Equal(["src_2"], report.Notable.Select(e => e.Post.RowKey));
    }

    [Fact]
    public void Recommended_may_repeat_a_main_post_and_is_capped_at_three()
    {
        var report = DigestReport.From(Items, Summary(
            headlines: [new("src_1", "З", "Суть", "Чому")],
            recommended: [new("src_1", "Читати"), new("src_2", "Читати"), new("src_3", "Читати"), new("src_4", "Читати")]));

        Assert.Equal(["src_1", "src_2", "src_3"], report.Recommended.Select(e => e.Post.RowKey));
        Assert.Equal(DigestReport.MaxRecommended, report.Recommended.Count);
    }

    [Fact]
    public void Posts_the_model_left_out_of_every_section_count_as_skipped()
    {
        var report = DigestReport.From(Items, Summary(
            headlines: [new("src_1", "З", "Суть", "Чому")],
            recommended: [new("src_2", "Читати")]));

        Assert.Equal(new DigestSkipped(Duplicates: 0, Other: 2), report.Skipped);
    }

    [Fact]
    public void Blank_overview_becomes_null_and_verification_items_are_trimmed_distinct_and_capped()
    {
        var report = DigestReport.From(Items, Summary(
            overview: "  ",
            needsVerification: [" Перевірити A ", "Перевірити A", "", "B", "C", "D", "E", "F"]));

        Assert.Null(report.Overview);
        Assert.Equal(["Перевірити A", "B", "C", "D", "E"], report.NeedsVerification);
    }

    [Fact]
    public void Links_and_other_post_data_stay_untouched()
    {
        var report = DigestReport.From(Items, Summary(headlines: [new("src_1", "З", "Суть", "Чому")]));

        Assert.Same(Items[0].Post, report.Headlines[0].Post);
    }

    [Fact]
    public void Plain_report_lists_the_posts_with_their_reasons_and_no_overview()
    {
        var report = DigestReport.Plain(Items);

        Assert.Null(report.Overview);
        Assert.Equal(4, report.Headlines.Count);
        Assert.Equal("Причина", report.Headlines[0].Text);
        Assert.Equal("Тема", report.Headlines[0].Title);
        Assert.Equal(DigestSkipped.None, report.Skipped);
    }

    [Fact]
    public void Extra_skipped_posts_are_added_to_the_count()
    {
        var report = DigestReport.Plain(Items).WithSkipped(new DigestSkipped(Duplicates: 2, Other: 3));

        Assert.Equal(new DigestSkipped(2, 3), report.Skipped);
        Assert.Equal(5, report.Skipped.Total);
    }
}
