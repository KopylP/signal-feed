using System.Globalization;
using System.Text;
using PostAgent.Shared.Notifications;

namespace PostAgent.Stats;

// The /stats message. Ukrainian: it goes to the end user. Source and topic names come from channels, so every one is escaped.
public static class StatsMessage
{
    public const string Usage = "Статистика: <code>/stats</code> — за 7 днів, <code>/stats 30</code> — за 30 (до того, скільки зберігаються підсумки).";

    public static string Empty(int days) => $"За {days} {DayWord(days)} даних ще немає: підсумок дня зберігається після того, як день закінчився.";

    public static string Format(StatsReport r, int requestedDays)
    {
        var text = new StringBuilder();
        text.Append("📊 <b>Статистика</b>: ").Append(r.Days).Append(' ').Append(DayWord(r.Days));
        if (r.Days < requestedDays)
        {
            text.Append(" з ").Append(requestedDays);
        }

        text.Append("\nПостів ").Append(r.Collected).Append(" · оцінено ").Append(r.Scored)
            .Append(" · цікавих ").Append(r.Interesting).Append(" (").Append(Percent(r.Scored == 0 ? 0 : (double)r.Interesting / r.Scored)).Append(')')
            .Append(" · середня оцінка ").Append(r.AverageScore.ToString("0.0", CultureInfo.InvariantCulture));

        Section(text, $"Найкорисніші джерела (частка цікавих, від {StatsReport.MinPostsToRank} постів)", r.Best);
        if (r.Weakest.Count > 0 && r.Weakest[0].Share < 1)
        {
            Section(text, "Найслабші джерела", r.Weakest.Where(s => !r.Best.Contains(s)).ToList());
        }

        if (r.Topics.Count > 0)
        {
            text.Append("\n\n<b>Теми</b>: ").Append(string.Join(", ", r.Topics.Select(t => $"{TelegramHtml.Escape(t.Topic)} ({t.Posts})")));
        }

        if (r.Usage.Count > 0)
        {
            text.Append("\n\n<b>Токени</b> (вхід / вихід, викликів):");
            foreach (var u in r.Usage)
            {
                text.Append('\n').Append(Purpose(u.Purpose)).Append(": ").Append(Tokens(u.InputTokens)).Append(" / ").Append(Tokens(u.OutputTokens)).Append(", ").Append(u.Calls);
            }
        }

        return text.ToString();
    }

    private static void Section(StringBuilder text, string title, IReadOnlyList<SourceRank> sources)
    {
        if (sources.Count == 0)
        {
            return;
        }

        text.Append("\n\n<b>").Append(title).Append("</b>");
        foreach (var s in sources)
        {
            text.Append("\n@").Append(TelegramHtml.Escape(s.Source)).Append(" — ").Append(Percent(s.Share))
                .Append(" (").Append(s.Interesting).Append(" з ").Append(s.Posts).Append(", оцінка ").Append(s.AverageScore.ToString("0.0", CultureInfo.InvariantCulture)).Append(')');
        }
    }

    private static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Purpose(string purpose) => purpose switch
    {
        "scoring" => "оцінювання і дайджест",
        "chat" => "чат",
        _ => TelegramHtml.Escape(purpose),
    };

    // 1234 -> 1.2K, 1234567 -> 1.2M
    private static string Tokens(long count) => count switch
    {
        >= 1_000_000 => (count / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (count / 1_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };

    private static string DayWord(int count) => (count % 100, count % 10) switch
    {
        ( >= 11 and <= 14, _) => "днів",
        (_, 1) => "день",
        (_, >= 2 and <= 4) => "дні",
        _ => "днів",
    };
}
