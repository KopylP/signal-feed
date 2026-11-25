namespace PostAgent.Login;

public static class LoginCommand
{
    private const string Command = "/login";

    // "/login" and "/login@bot_name", with nothing after it.
    public static bool Is(string? text)
    {
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts is [var command] && command.Split('@', 2)[0].Equals(Command, StringComparison.OrdinalIgnoreCase);
    }
}
