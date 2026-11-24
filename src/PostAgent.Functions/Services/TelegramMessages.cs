using System.Text;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Тексти повідомлень для Telegram (parse_mode HTML, весь динамічний текст екранується).</summary>
public static class TelegramMessages
{
    public const string NoPosts = "За добу нових постів не було";

    private const int MaxReasonChars = 300;

    public static string Escape(string? text) =>
        (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string FormatDigest(IReadOnlyList<DigestItem> items)
    {
        var sb = new StringBuilder("📰 <b>Дайджест</b>");
        foreach (var item in items)
        {
            var label = item.IsInteresting ? "🔥 <b>Цікаве</b>" : "📌 <b>Найкраще за день</b>";
            sb.Append("\n\n").Append(label).Append(" · ").Append(Escape(item.Post.Topic));
            AppendDetails(sb, item.Post);
        }
        return sb.ToString();
    }

    public static string FormatAlert(IReadOnlyList<PostEntity> posts)
    {
        var sb = new StringBuilder("🚨 <b>Щось відбувається</b>");
        foreach (var post in posts)
        {
            sb.Append("\n\n<b>").Append(Escape(post.Topic)).Append("</b>");
            AppendDetails(sb, post);
        }
        return sb.ToString();
    }

    private static void AppendDetails(StringBuilder sb, PostEntity post)
    {
        sb.Append("\n@").Append(Escape(post.Source));
        if (post.MessageCount > 1)
        {
            sb.Append(" · ").Append(post.MessageCount).Append(" повідомлень");
        }
        else if (post.Author is not null)
        {
            sb.Append(" · ").Append(Escape(post.Author));
        }

        var reason = post.Reason ?? "";
        if (reason.Length > MaxReasonChars)
        {
            reason = reason[..MaxReasonChars] + "…";
        }
        sb.Append('\n').Append(Escape(reason));
        sb.Append("\n<a href=\"").Append(Escape(post.Url).Replace("\"", "&quot;")).Append("\">Читати</a>");
    }
}
