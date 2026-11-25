namespace PostAgent.Login;

// Where a sign-in stands after a step, as far as the bot needs to know.
public enum LoginStep
{
    PhoneRequired,
    CodeRequired,
    CodeInvalid,
    PasswordRequired,
    PasswordInvalid,
    Done,

    // Telegram asks for something the bot does not do (sign-up, a login email): TgLogin handles it.
    Unsupported,
}
