using PostAgent.Shared.Chat;

namespace PostAgent.Login;

public static class LoginCommand
{
    private const string Command = "/login";

    // "/login" and "/login@bot_name", with nothing after it.
    public static bool Is(string? text) => BotCommandText.Parse(text) is { Name: Command, Arguments: "" };
}
