using PostAgent.Shared.Chat;

namespace PostAgent.Pairing;

public static class StartCommand
{
    private const string Command = "/start";

    // True for the command with or without a key, so that a bare or mistyped /start never reaches the chat as a question.
    public static bool IsStart(string? text) => BotCommandText.Parse(text) is { Name: Command };

    // Accepts "/start <key>" and "/start@bot_name <key>" (what Telegram sends in groups and from a deep link).
    public static string? ParseKey(string? text) =>
        BotCommandText.Parse(text) is { Name: Command, Arguments: { Length: > 0 } key } ? key : null;
}
