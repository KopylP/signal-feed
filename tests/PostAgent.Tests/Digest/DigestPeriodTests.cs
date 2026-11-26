using PostAgent.Digest;

namespace PostAgent.Tests.Digest;

public class DigestPeriodTests
{
    // 2026-11-24 08:00 UTC is 10:00 in Kyiv (UTC+02:00).
    private static readonly DateTimeOffset Now = TestData.Now;
    private static readonly TimeZoneInfo Kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    private static (DateTimeOffset Since, DateTimeOffset Until)? Parse(string? text, int retentionDays = 3)
    {
        var period = DigestPeriod.Parse(text, Now, Kyiv, retentionDays);
        return period is { } p ? (p.Since, p.Until) : null;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void No_arguments_means_the_last_day(string? text) =>
        Assert.Equal((Now.AddHours(-24), Now), Parse(text));

    [Theory]
    [InlineData("6", 6)]
    [InlineData("6h", 6)]
    [InlineData("12H", 12)]
    public void A_number_is_hours_back(string text, int hours) =>
        Assert.Equal((Now.AddHours(-hours), Now), Parse(text));

    [Fact]
    public void Hours_never_reach_further_than_the_retention() =>
        Assert.Equal((Now.AddDays(-2), Now), Parse("500", retentionDays: 2));

    [Theory]
    [InlineData("today")]
    [InlineData("Сьогодні")]
    public void Today_starts_at_midnight_of_the_reader(string text) =>
        Assert.Equal((new DateTimeOffset(2026, 11, 23, 22, 0, 0, TimeSpan.Zero), Now), Parse(text));

    [Theory]
    [InlineData("yesterday")]
    [InlineData("вчора")]
    public void Yesterday_is_the_whole_previous_day_of_the_reader(string text) =>
        Assert.Equal((new DateTimeOffset(2026, 11, 22, 22, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 23, 22, 0, 0, TimeSpan.Zero)), Parse(text));

    [Fact]
    public void Two_values_give_an_exact_period_in_the_reader_time_zone() =>
        Assert.Equal((new DateTimeOffset(2026, 11, 23, 16, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 23, 21, 0, 0, TimeSpan.Zero)), Parse("2026-11-23T18:00 2026-11-23T23:00"));

    [Fact]
    public void A_date_alone_is_that_whole_day() =>
        Assert.Equal((new DateTimeOffset(2026, 11, 22, 22, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 23, 22, 0, 0, TimeSpan.Zero)), Parse("2026-11-23 2026-11-23"));

    [Fact]
    public void A_period_never_ends_after_now() =>
        Assert.Equal(Now, Parse("2026-11-24T00:00 2026-12-31")!.Value.Until);

    [Theory]
    [InlineData("hello")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1 2 3")]
    [InlineData("2026-11-23T18:00 nonsense")]
    [InlineData("2026-10-01 2026-10-02")] // before the retention: nothing is stored
    public void Anything_else_is_not_a_period(string text) => Assert.Null(Parse(text));
}
