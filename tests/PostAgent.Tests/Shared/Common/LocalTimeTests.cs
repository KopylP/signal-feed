using PostAgent.Shared.Common;

namespace PostAgent.Tests.Shared.Common;

public class LocalTimeTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Europe/Kyiv", "2026-11-24 10:00 (UTC+02:00)")]
    [InlineData("UTC", "2026-11-24 08:00 (UTC)")]
    [InlineData("Asia/Kolkata", "2026-11-24 13:30 (UTC+05:30)")]
    [InlineData("America/New_York", "2026-11-24 03:00 (UTC-05:00)")]
    public void Shows_the_wall_clock_and_the_offset_of_the_zone(string id, string expected) =>
        Assert.Equal(expected, LocalTime.StampWithOffset(Moment, LocalTime.Zone(id)));

    [Theory]
    [InlineData("Europe/Nowhere")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_zone_falls_back_to_UTC(string? id) =>
        Assert.Equal(TimeZoneInfo.Utc, LocalTime.Zone(id));

    [Fact]
    public void The_offset_follows_daylight_saving_time() =>
        Assert.Equal("UTC+03:00", LocalTime.Offset(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero), LocalTime.Zone("Europe/Kyiv")));
}
