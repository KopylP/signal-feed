namespace PostAgent.Telegram;

/// <summary>Де лежить сесія Telegram. Цей файл підключається і в TgLogin, щоб імена не розходилися.</summary>
public static class SessionBlob
{
    public const string Name = "wtelegram.session";
    public const string DefaultContainer = "tg-session";
}
