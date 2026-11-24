
namespace PostAgent.Tests;

public class QuietHoursTests
{
    private const string Kyiv = "Europe/Kyiv";

    // 24 листопада Київ живе за UTC+2.
    private static DateTimeOffset Utc(int hour, int minute = 0) => new(2026, 11, 24, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_spec_means_no_quiet_hours(string spec)
    {
        Assert.False(QuietHours.IsQuiet(spec, Kyiv, Utc(2)));
    }

    [Theory]
    [InlineData(21, 0, true)]   // 23:00 за Києвом
    [InlineData(23, 30, true)]  // 01:30
    [InlineData(4, 59, true)]   // 06:59
    [InlineData(5, 0, false)]   // 07:00, кінець інтервалу не входить
    [InlineData(12, 0, false)]
    [InlineData(20, 59, false)] // 22:59
    public void Interval_across_midnight(int utcHour, int utcMinute, bool expected)
    {
        Assert.Equal(expected, QuietHours.IsQuiet("23:00-07:00", Kyiv, Utc(utcHour, utcMinute)));
    }

    [Theory]
    [InlineData(8, true)]   // 10:00
    [InlineData(10, false)] // 12:00
    public void Interval_within_a_day(int utcHour, bool expected)
    {
        Assert.Equal(expected, QuietHours.IsQuiet("09:00-11:00", Kyiv, Utc(utcHour)));
    }

    [Fact]
    public void Invalid_spec_throws()
    {
        Assert.Throws<FormatException>(() => QuietHours.IsQuiet("night", Kyiv, Utc(1)));
    }

    [Theory]
    [InlineData(19, 59, false)] // 22:59 за літнім часом (UTC+3)
    [InlineData(20, 0, true)]   // 23:00
    [InlineData(3, 59, true)]   // 06:59
    [InlineData(4, 0, false)]   // 07:00
    public void Summer_time_uses_the_zone_offset_of_that_date(int utcHour, int utcMinute, bool expected)
    {
        var summerDay = new DateTimeOffset(2026, 7, 15, utcHour, utcMinute, 0, TimeSpan.Zero);

        Assert.Equal(expected, QuietHours.IsQuiet("23:00-07:00", Kyiv, summerDay));
    }

    [Theory]
    [InlineData("")]
    [InlineData("23:00-07:00")]
    public void Validate_accepts_empty_and_correct_settings(string spec)
    {
        Assert.Null(QuietHours.Validate(spec, Kyiv));
    }

    [Theory]
    [InlineData("night", Kyiv)]
    [InlineData("23-07-01", Kyiv)]
    [InlineData("23:00-07:00", "Europe/Nowhere")]
    public void Validate_describes_what_is_wrong(string spec, string zone)
    {
        Assert.NotNull(QuietHours.Validate(spec, zone));
    }
}
