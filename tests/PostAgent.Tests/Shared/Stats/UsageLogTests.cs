using PostAgent.Shared.Stats;

namespace PostAgent.Tests.Shared.Stats;

public class UsageLogTests
{
    private static readonly DateOnly Day = new(2026, 11, 24);

    [Fact]
    public void Adds_to_the_same_day_and_purpose_and_keeps_others_apart()
    {
        var rows = UsageLog.Add([], Day, "chat", 100, 10);
        rows = UsageLog.Add(rows, Day, "chat", 50, 5);
        rows = UsageLog.Add(rows, Day, "scoring", 7, 1);

        Assert.Equal(new UsageDay("2026-11-24", "chat", 2, 150, 15), rows.Single(r => r.Purpose == "chat"));
        Assert.Equal(new UsageDay("2026-11-24", "scoring", 1, 7, 1), rows.Single(r => r.Purpose == "scoring"));
    }

    [Fact]
    public void Forgets_days_older_than_the_kept_period()
    {
        var rows = UsageLog.Add([], Day.AddDays(-UsageLog.KeepDays), "chat", 1, 1);
        rows = UsageLog.Add(rows, Day, "chat", 1, 1);

        Assert.Equal(["2026-11-24"], rows.Select(r => r.Day));
    }
}
