namespace PostAgent.Shared.Notifications;

public sealed record InlineKeyboard(IReadOnlyList<IReadOnlyList<KeyboardButton>> Rows)
{
    public static readonly InlineKeyboard Empty = new([]);
}
