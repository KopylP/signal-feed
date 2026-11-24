using System.Text;
using PostAgent.Common;
using PostAgent.Notifications;

namespace PostAgent.Digest;

public static class DigestMessage
{
    public const string NoPosts = "За добу нових постів не було";

    private const int MaxOverviewChars = 400;

    public static string Format(DigestReport report)
    {
        var message = new StringBuilder("📰 <b>Дайджест</b>");
        if (report.Overview is not null)
        {
            message.Append("\n\n").Append(TelegramHtml.Escape(report.Overview.Truncate(MaxOverviewChars, "…")));
        }

        foreach (var item in report.Items)
        {
            var label = item.IsInteresting ? "🔥 <b>Цікаве</b>" : "📌 <b>Найкраще за день</b>";
            message.Append("\n\n").Append(label).Append(" · ").Append(TelegramHtml.Escape(TelegramHtml.Topic(item.Post)));
            TelegramHtml.AppendPostDetails(message, item.Post, item.Insight ?? item.Post.Reason);
        }
        return message.ToString();
    }
}
