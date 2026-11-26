namespace PostAgent.Shared.Notifications;

// Buttons in place of the reader's keyboard: a tap sends the button's text as the reader's own message, so whoever reads messages
// gets the choice as ordinary text and nothing has to be stored. No rows means "take such buttons away".
public sealed record ReplyKeyboard(IReadOnlyList<IReadOnlyList<string>> Rows)
{
    // Options up to this length fit two to a row on a phone.
    public const int ShortOptionChars = 20;

    public static readonly ReplyKeyboard Remove = new([]);

    public bool IsRemove => Rows.Count == 0;

    // Two buttons to a row when every option is short, one otherwise, in the given order.
    public static ReplyKeyboard Of(IReadOnlyList<string> options)
    {
        var perRow = options.All(o => o.Length <= ShortOptionChars) ? 2 : 1;
        return new(options.Chunk(perRow).Select(row => (IReadOnlyList<string>)row).ToList());
    }
}
