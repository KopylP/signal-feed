namespace PostAgent.Pairing;

public static class StartCommand
{
    private const string Command = "/start";

    // True for the command with or without a key, so that a bare or mistyped /start never reaches the chat as a question.
    public static bool IsStart(string? text)
    {
        var command = text?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return command is not null && command.Split('@', 2)[0].Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    // Accepts "/start <key>" and "/start@bot_name <key>" (what Telegram sends in groups and from a deep link).
    public static string? ParseKey(string? text)
    {
        var parts = text?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not [var command, var key])
        {
            return null;
        }

        var name = command.Split('@', 2)[0];
        return name.Equals(Command, StringComparison.OrdinalIgnoreCase) ? key : null;
    }
}
