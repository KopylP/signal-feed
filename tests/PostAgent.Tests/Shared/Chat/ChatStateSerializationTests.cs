using System.Text.Json;
using PostAgent.Shared.Chat;

namespace PostAgent.Tests.Shared.Chat;

public class ChatStateSerializationTests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 25, 7, 0, 0, TimeSpan.Zero);

    private static ReportSnapshot Report() => new(
        "20261125070000",
        Created,
        "Огляд",
        [new SnapshotEntry("Headlines", "2026-11-25/news_1", "news", Created, "Заголовок", "Текст", "Чому", "Уривок", 9, "https://t.me/news/1")],
        ["Перевірити"]);

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public void A_state_survives_a_round_trip()
    {
        var state = new ChatState(Report(), [10, 11], Json("""{"stateBag":{}}"""));

        Assert.True(ChatStateJson.TryRead(ChatStateJson.Write(state), out var read));

        Assert.Equal(state.Report.ReportId, read.Report.ReportId);
        Assert.Equal(state.Report.Entries, read.Report.Entries);
        Assert.Equal(state.Report.NeedsVerification, read.Report.NeedsVerification);
        Assert.Equal([10L, 11L], read.ProcessedUpdateIds);
        Assert.Equal("""{"stateBag":{}}""", read.Session?.GetRawText());
    }

    [Fact]
    public void WithProcessed_appends_the_id_and_keeps_only_the_last_20()
    {
        var state = new ChatState(Report(), Enumerable.Range(1, 20).Select(i => (long)i).ToList(), null);

        var next = state.WithProcessed(21, Json("{}"));

        Assert.Equal(20, next.ProcessedUpdateIds.Count);
        Assert.Equal(2L, next.ProcessedUpdateIds[0]);
        Assert.Equal(21L, next.ProcessedUpdateIds[^1]);
    }

    [Fact]
    public void WithProcessed_stores_the_new_session_and_leaves_the_old_state_alone()
    {
        var state = new ChatState(Report(), [], null);

        var next = state.WithProcessed(5, Json("""{"a":1}"""));

        Assert.Null(state.Session);
        Assert.Empty(state.ProcessedUpdateIds);
        Assert.Equal("""{"a":1}""", next.Session?.GetRawText());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"report":"not an object"}""")]
    [InlineData("""{"report":{}}""")]
    [InlineData("""{"report":{"reportId":"r","createdAt":"2026-11-25T07:00:00+00:00","overview":null,"entries":[]}}""")]
    [InlineData("""{"report":{"createdAt":"2026-11-25T07:00:00+00:00","entries":[],"needsVerification":[]}}""")]
    public void An_unreadable_snapshot_reads_as_missing(string raw)
    {
        Assert.False(ChatStateJson.TryRead(raw, out _));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void A_session_of_the_wrong_shape_reads_as_no_session_with_the_snapshot_intact(string session)
    {
        var good = ChatStateJson.Write(new ChatState(Report(), [7], Json("{}")));
        using var doc = JsonDocument.Parse(good);
        var report = doc.RootElement.GetProperty("report").GetRawText();
        var raw = $$"""{"report":{{report}},"processedUpdateIds":[7],"session":{{session}}}""";

        Assert.True(ChatStateJson.TryRead(raw, out var read));

        Assert.Null(read.Session);
        Assert.Equal("20261125070000", read.Report.ReportId);
        Assert.Equal([7L], read.ProcessedUpdateIds);
    }

    [Fact]
    public void Missing_processed_ids_read_as_an_empty_list()
    {
        var good = ChatStateJson.Write(new ChatState(Report(), [7], null));
        using var doc = JsonDocument.Parse(good);
        var raw = $$"""{"report":{{doc.RootElement.GetProperty("report").GetRawText()}}}""";

        Assert.True(ChatStateJson.TryRead(raw, out var read));
        Assert.Empty(read.ProcessedUpdateIds);
    }
}
