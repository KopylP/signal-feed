namespace PostAgent.Shared.Chat;

// A message that is a bot command: "/name args" or "/name@bot_name args" (what Telegram sends in groups and from a deep link).
// Name is lower case without the bot name; Arguments is the trimmed rest, empty when there is none.
public sealed record BotCommandText(string Name, string Arguments)
{
    // Null when the text is not a command.
    public static BotCommandText? Parse(string? text)
    {
        var parts = text?.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not [var command, ..] || !command.StartsWith('/'))
        {
            return null;
        }

        return new BotCommandText(command.Split('@', 2)[0].ToLowerInvariant(), parts.Length > 1 ? parts[1] : string.Empty);
    }
}
