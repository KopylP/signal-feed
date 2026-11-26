using System.Text;
using PostAgent.Digest;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Feedback;

namespace PostAgent.Tests.Digest;

public class DigestFeedbackMessageTests
{
    private static SnapshotEntry Entry(string id, string? title = "Заголовок") =>
        new("Headlines", id, "src", TestData.Now, title, "Суть", null, "Уривок", 9, "https://t.me/src/1");

    [Fact]
    public void Each_entry_gets_a_row_with_a_like_that_names_it_and_a_dislike()
    {
        var report = new ReportSnapshot("r1", TestData.Now, null, [Entry("d/a", "Перший"), Entry("d/b", "Другий")], []);

        var (html, keyboard) = DigestFeedbackMessage.Build(report)!.Value;

        Assert.Equal(FeedbackPrompt.Message, html);
        Assert.Equal(2, keyboard.Rows.Count);
        Assert.Equal(["👍 Перший", "👎"], keyboard.Rows[0].Select(b => b.Text));
        Assert.Equal(FeedbackCallback.Encode("r1", 1, FeedbackVote.Down), keyboard.Rows[1][1].Data);
    }

    [Fact]
    public void An_entry_without_a_title_is_named_by_the_start_of_its_text()
    {
        var report = new ReportSnapshot("r1", TestData.Now, null, [Entry("d/a", null)], []);

        var (_, keyboard) = DigestFeedbackMessage.Build(report)!.Value;

        Assert.Equal("👍 Суть", keyboard.Rows[0][0].Text);
    }

    [Fact]
    public void A_post_shown_twice_has_one_row_pointing_at_its_first_place()
    {
        var report = new ReportSnapshot("r1", TestData.Now, null, [Entry("d/a"), Entry("d/b"), Entry("d/a")], []);

        var (_, keyboard) = DigestFeedbackMessage.Build(report)!.Value;

        Assert.Equal(2, keyboard.Rows.Count);
        Assert.Equal(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up), keyboard.Rows[0][0].Data);
    }

    [Fact]
    public void A_long_title_is_cut_and_every_button_fits_the_telegram_limit()
    {
        var report = new ReportSnapshot("20261126080000", TestData.Now, null, [Entry("d/a", new string('я', 200))], []);

        var (_, keyboard) = DigestFeedbackMessage.Build(report)!.Value;

        Assert.True(keyboard.Rows[0][0].Text.Length < 40);
        Assert.All(keyboard.Rows.SelectMany(r => r), b => Assert.True(Encoding.UTF8.GetByteCount(b.Data) <= 64));
    }

    [Fact]
    public void A_digest_without_entries_has_nothing_to_rate() =>
        Assert.Null(DigestFeedbackMessage.Build(new ReportSnapshot("r1", TestData.Now, null, [], [])));
}
