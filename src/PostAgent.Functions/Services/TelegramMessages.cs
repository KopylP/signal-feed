using System.Net;
using System.Text;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Тексти повідомлень для Telegram (parse_mode HTML, весь динамічний текст екранується).</summary>
public static class TelegramMessages
{
    public const string NoPosts = "За добу нових постів не було";

    /// <summary>
    /// Ліміт записів у одному повідомленні. Запис не перевищує ~450 символів (тема 60, причина 200,
    /// джерело й посилання), тож 8 записів гарантовано вкладаються в 4096 символів Telegram.
    /// </summary>
    public const int MaxItems = 8;

    private const int MaxTopicChars = 60;
    private const int MaxReasonChars = 200;

    public static string Escape(string? text) => WebUtility.HtmlEncode(text ?? "");

    public static string FormatDigest(IReadOnlyList<DigestItem> items)
    {
        var message = new StringBuilder("📰 <b>Дайджест</b>");
        foreach (var item in items)
        {
            var label = item.IsInteresting ? "🔥 <b>Цікаве</b>" : "📌 <b>Найкраще за день</b>";
            message.Append("\n\n").Append(label).Append(" · ").Append(Escape(Topic(item.Post)));
            AppendDetails(message, item.Post);
        }
        return message.ToString();
    }

    public static string FormatAlert(IReadOnlyList<PostEntity> posts)
    {
        var message = new StringBuilder("🚨 <b>Щось відбувається</b>");
        foreach (var post in posts)
        {
            message.Append("\n\n<b>").Append(Escape(Topic(post))).Append("</b>");
            AppendDetails(message, post);
        }
        return message.ToString();
    }

    private static string Topic(PostEntity post) => (post.Topic ?? "").Truncate(MaxTopicChars, "…");

    private static void AppendDetails(StringBuilder message, PostEntity post)
    {
        message.Append('\n').Append(SourceLine(post));
        message.Append('\n').Append(Escape((post.Reason ?? "").Truncate(MaxReasonChars, "…")));
        message.Append("\n<a href=\"").Append(Escape(post.Url)).Append("\">Читати</a>");
    }

    /// <summary>"@джерело" плюс кількість повідомлень (для пачки) або автор (для повідомлення групи).</summary>
    private static string SourceLine(PostEntity post)
    {
        var details = post.MessageCount > 1 ? $"{post.MessageCount} повідомлень" : post.Author;
        return details is null ? $"@{Escape(post.Source)}" : $"@{Escape(post.Source)} · {Escape(details)}";
    }
}
