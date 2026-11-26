using PostAgent.Shared.Stats;
using PostAgent.Stats;

namespace PostAgent.Tests.Stats;

public class StatsMessageTests
{
    private static StatsReport Report(IReadOnlyList<SourceRank>? best = null, IReadOnlyList<TopicStats>? topics = null, IReadOnlyList<UsageTotal>? usage = null) =>
        new(7, 400, 380, 50, 4.84, best ?? [], best ?? [], topics ?? [], usage ?? []);

    [Fact]
    public void Shows_the_totals_with_the_share_of_interesting_posts()
    {
        var text = StatsMessage.Format(Report(), 7);

        Assert.Contains("Статистика", text);
        Assert.Contains("Постів 400 · оцінено 380 · цікавих 50 (13%) · середня оцінка 4.8", text);
        Assert.DoesNotContain("з 7", text);
    }

    [Fact]
    public void Says_how_many_of_the_asked_days_have_data()
    {
        Assert.Contains("3 дні з 7", StatsMessage.Format(Report() with { Days = 3 }, 7));
    }

    [Fact]
    public void Names_and_topics_from_channels_are_escaped()
    {
        var text = StatsMessage.Format(Report(
            best: [new SourceRank("<b>evil", 10, 5, 7.0)],
            topics: [new TopicStats("<script>", 3, 5.0)]), 7);

        Assert.DoesNotContain("<b>evil", text);
        Assert.DoesNotContain("<script>", text);
        Assert.Contains("&lt;b&gt;evil", text);
    }

    [Fact]
    public void Tokens_are_shortened_and_the_purposes_named_for_the_reader()
    {
        var text = StatsMessage.Format(Report(usage: [new UsageTotal("scoring", 12, 1_234_567, 98_000), new UsageTotal("chat", 3, 900, 50)]), 7);

        Assert.Contains("оцінювання і дайджест: 1.2M / 98.0K, 12", text);
        Assert.Contains("чат: 900 / 50, 3", text);
    }

    [Theory]
    [InlineData(1, "день")]
    [InlineData(3, "дні")]
    [InlineData(7, "днів")]
    [InlineData(11, "днів")]
    [InlineData(21, "день")]
    public void Counts_days_in_correct_ukrainian(int days, string word) =>
        Assert.Contains($"{days} {word}", StatsMessage.Empty(days));
}
