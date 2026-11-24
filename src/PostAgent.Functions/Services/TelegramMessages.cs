using System.Net;
using System.Text;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Тексти повідомлень для Telegram (parse_mode HTML, весь динамічний текст екранується).</summary>
public static class TelegramMessages
{
    public const string NoPosts = "За добу нових постів не було";

    /// <summary>
    /// Ліміт записів у одному повідомленні. Запис не перевищує ~450 символів (тема 60, пояснення 200,
    /// джерело й посилання), підсумок до 400, тож 8 записів вкладаються в 4096 символів Telegram.
    /// </summary>
    public const int MaxItems = 8;

    private const int MaxTopicChars = 60;
    private const int MaxDetailChars = 200;
    private const int MaxOverviewChars = 400;

    public static string Escape(string? text) => WebUtility.HtmlEncode(text ?? "");

    public static string FormatDigest(DigestReport report)
    {
        var message = new StringBuilder("📰 <b>Дайджест</b>");
        if (report.Overview is not null)
        {
            message.Append("\n\n").Append(Escape(report.Overview.Truncate(MaxOverviewChars, "…")));
        }

        foreach (var item in report.Items)
        {
            var label = item.IsInteresting ? "🔥 <b>Цікаве</b>" : "📌 <b>Найкраще за день</b>";
            message.Append("\n\n").Append(label).Append(" · ").Append(Escape(Topic(item.Post)));
            AppendDetails(message, item.Post, item.Insight ?? item.Post.Reason);
        }
        return message.ToString();
    }

    public static string FormatAlert(IReadOnlyList<PostEntity> posts)
    {
        var message = new StringBuilder("🚨 <b>Щось відбувається</b>");
        foreach (var post in posts)
        {
            message.Append("\n\n<b>").Append(Escape(Topic(post))).Append("</b>");
            AppendDetails(message, post, post.Reason);
        }
        return message.ToString();
    }

    public static string FormatHealth(IReadOnlyList<string> failures) =>
        "⚠️ <b>Збір не вдався</b>\n" + string.Join('\n', failures.Select(Escape));

    private static string Topic(PostEntity post) => (post.Topic ?? "").Truncate(MaxTopicChars, "…");

    private static void AppendDetails(StringBuilder message, PostEntity post, string? detail)
    {
        message.Append('\n').Append(SourceLine(post));
        message.Append('\n').Append(Escape((detail ?? "").Truncate(MaxDetailChars, "…")));
        message.Append("\n<a href=\"").Append(Escape(post.Url)).Append("\">Читати</a>");
    }

    /// <summary>"@джерело" плюс кількість повідомлень (для пачки) або автор (для повідомлення групи).</summary>
    private static string SourceLine(PostEntity post)
    {
        var details = post.MessageCount > 1 ? $"{post.MessageCount} повідомлень" : post.Author;
        return details is null ? $"@{Escape(post.Source)}" : $"@{Escape(post.Source)} · {Escape(details)}";
    }
}
