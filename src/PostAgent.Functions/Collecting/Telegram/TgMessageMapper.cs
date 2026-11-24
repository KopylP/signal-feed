using TL;
using PostAgent.Shared.Posts;

namespace PostAgent.Collecting.Telegram;

// Перетворює повідомлення бібліотеки WTelegramClient на наш TgMessage.
public static class TgMessageMapper
{
    public static TgMessage Map(MessageBase message, Messages_MessagesBase page, SourceType type)
    {
        var date = new DateTimeOffset(DateTime.SpecifyKind(message.Date, DateTimeKind.Utc));
        if (message is not Message m)
        {
            return new TgMessage(message.ID, date, null, null, 0, IsService: true);
        }

        var author = type == SourceType.Group ? AuthorName(page, m) : null;
        return new TgMessage(m.id, date, m.message, author, Engagement(m), IsService: false);
    }

    // Реакції + 2 × пересилання + відповіді: чим більше, тим помітніше повідомлення.
    private static int Engagement(Message m) =>
        (m.reactions?.results?.Sum(r => r.count) ?? 0) + 2 * m.forwards + (m.replies?.replies ?? 0);

    private static string? AuthorName(Messages_MessagesBase page, Message m)
    {
        if (m.from_id is null || page.UserOrChat(m.from_id) is not User user)
        {
            return null;
        }
        return user.MainUsername ?? $"{user.first_name} {user.last_name}".Trim();
    }
}
