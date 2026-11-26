using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Common;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;

namespace PostAgent.Chat.Agent.Tools;

// The long memory: one summary per day, kept StatsRetentionDays, with no post text. Topics and titles come from channels, so the whole answer is one data block.
public sealed class GetTrendsTool(IDailyStatsStore store, IEffectiveOptions options, TimeProvider time) : IChatTool
{
    private const int MaxChars = 6000;
    private const int WeeklyFromDays = 22;

    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        (
            [Description("How many days back, at least 1 (the daily summaries are kept for a long time, 90 by default). Default 14.")] int? days = null,
            [Description("Optional words: only topics that contain every word, for example: core update.")] string? topic = null,
            CancellationToken ct = default) => RunAsync(days ?? 14, topic, ct),
        new AIFunctionFactoryOptions
        {
            Name = "get_trends",
            Description = "Trends over weeks, from the daily summaries (counts per day, per source and per topic, best posts, no post text). "
                + "Use it for questions about change over time («що змінилось за місяць у темі X»). For the text of recent posts use search_posts.",
        });

    private async Task<string> RunAsync(int days, string? topic, CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        days = Math.Clamp(days, 1, settings.StatsRetentionDays);
        var stored = await store.LoadAsync(today.AddDays(-days), today.AddDays(-1), ct);
        if (stored.Count == 0)
        {
            return "No daily summaries yet: they are written once a day, after a day is over.";
        }

        var terms = (topic ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var rows = (days >= WeeklyFromDays ? ByWeek(stored) : stored.Select(d => (d.Day, d.Collected, d.Scored, d.Interesting, d.AverageScore, d.Topics, d.Top))).ToList();

        var body = new StringBuilder("Days are UTC dates of publication; the figures cover only what was collected. A week with fewer than 7 days is partial: compare rows by the posts per stored day, not by the totals.\n");
        foreach (var (label, collected, scored, interesting, average, topics, top) in rows)
        {
            var matching = topics.Where(t => terms.All(w => t.Topic.Contains(w, StringComparison.OrdinalIgnoreCase))).ToList();
            if (terms.Length > 0 && matching.Count == 0)
            {
                body.Append(label).AppendLine(": no matching topic");
                continue;
            }

            body.Append(label).Append(": ").Append(collected).Append(" posts, ").Append(scored).Append(" scored, ").Append(interesting)
                .Append(" interesting, average ").Append(average.ToString("0.0", CultureInfo.InvariantCulture));
            var shown = (terms.Length > 0 ? matching : topics.Take(5).ToList()).Take(5);
            body.Append(" | topics: ").AppendLine(string.Join(", ", shown.Select(t => $"{t.Topic} ({t.Posts}, avg {t.AverageScore.ToString("0.0", CultureInfo.InvariantCulture)})")));
            foreach (var p in top.Where(p => terms.Length == 0 || terms.All(w => (p.Topic ?? string.Empty).Contains(w, StringComparison.OrdinalIgnoreCase))).Take(2))
            {
                body.Append("  best: @").Append(p.Source).Append(' ').Append(p.Score).Append(" - ").Append(p.Topic ?? "-").Append(' ').AppendLine(p.Url);
            }
        }

        return DataBlock.Wrap("trends", body.ToString().Trim().Truncate(MaxChars, "…"));
    }

    // Weeks start on Monday; each row sums its days, and the topics and best posts of the days are merged.
    private static IEnumerable<(string Label, int Collected, int Scored, int Interesting, double Average, IReadOnlyList<TopicStats> Topics, IReadOnlyList<TopPost> Top)> ByWeek(IReadOnlyList<DailyStats> days) =>
        days.GroupBy(d => WeekStart(DateOnly.ParseExact(d.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .OrderBy(g => g.Key)
            .Select(g => (
                $"week of {g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} ({g.Count()} of 7 days stored)",
                g.Sum(d => d.Collected),
                g.Sum(d => d.Scored),
                g.Sum(d => d.Interesting),
                g.Sum(d => d.Scored) == 0 ? 0 : g.Sum(d => d.AverageScore * d.Scored) / g.Sum(d => d.Scored),
                (IReadOnlyList<TopicStats>)g.SelectMany(d => d.Topics).GroupBy(t => t.Topic)
                    .Select(t => new TopicStats(t.Key, t.Sum(x => x.Posts), t.Sum(x => x.AverageScore * x.Posts) / Math.Max(1, t.Sum(x => x.Posts))))
                    .OrderByDescending(t => t.Posts).ThenBy(t => t.Topic, StringComparer.Ordinal).ToList(),
                (IReadOnlyList<TopPost>)g.SelectMany(d => d.Top).OrderByDescending(p => p.Score).ThenBy(p => p.PostId, StringComparer.Ordinal).Take(3).ToList()));

    private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
