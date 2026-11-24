namespace PostAgent.Pairing;

public static class StartCommand
{
    private const string Command = "/start";

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
