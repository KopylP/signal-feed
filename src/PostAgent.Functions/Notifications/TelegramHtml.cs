using System.Net;
using System.Text;
using PostAgent.Common;
using PostAgent.Posts;

namespace PostAgent.Notifications;

public static class TelegramHtml
{
    private const int MaxTopicChars = 60;
    private const int MaxDetailChars = 200;

    public static string Escape(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    public static string Topic(PostEntity post) => (post.Topic ?? string.Empty).Truncate(MaxTopicChars, "…");

    // Спільний блок запису для дайджесту й алерта: джерело, пояснення, посилання.
    public static void AppendPostDetails(StringBuilder message, PostEntity post, string? detail)
    {
        message.Append('\n').Append(SourceLine(post));
        message.Append('\n').Append(Escape((detail ?? string.Empty).Truncate(MaxDetailChars, "…")));
        message.Append("\n<a href=\"").Append(Escape(post.Url)).Append("\">Читати</a>");
    }

    // "@джерело" плюс кількість повідомлень (для пачки) або автор (для повідомлення групи).
    private static string SourceLine(PostEntity post)
    {
        var details = post.MessageCount > 1 ? $"{post.MessageCount} повідомлень" : post.Author;
        return details is null ? $"@{Escape(post.Source)}" : $"@{Escape(post.Source)} · {Escape(details)}";
    }
}
