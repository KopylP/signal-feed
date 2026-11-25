using PostAgent.Shared.Notifications;

namespace PostAgent.Login;

// What the bot says during a sign-in (Ukrainian: the owner reads it).
public static class LoginTexts
{
    public const string Starting = "⏳ Запитую в Telegram код входу…";
    public const string InProgress = "Вхід уже триває. Завершіть його або зачекайте кілька хвилин, поки він скінчиться.";
    public const string Busy = "Зараз іде збір, сесія зайнята. Надішліть /login ще раз за хвилину.";
    public const string NoHost = "Вхід через бота тут недоступний: невідома адреса Function App. Увійдіть через TgLogin.";
    public const string Button = "🔑 Ввести код";
    public const string WrongCode = "Код не підійшов. Натисніть «Ввести код» ще раз.";
    public const string PasswordNeeded = "Потрібен пароль двоетапної перевірки. Натисніть «Ввести код» і введіть пароль.";
    public const string WrongPassword = "Пароль не підійшов. Натисніть «Ввести код» і введіть його ще раз.";
    public const string TooManyTries = "Забагато невдалих спроб. Почніть знову з /login.";
    public const string Expired = "Час на вхід вийшов. Почніть знову з /login.";
    public const string Unsupported = "Telegram просить те, чого бот не вміє (реєстрацію або пошту для входу). Увійдіть через TgLogin.";

    public static string CodeSent(int minutes) =>
        "🔑 Telegram надіслав код входу в застосунок акаунта-читача (чат «Telegram»).\n"
        + "Натисніть кнопку і введіть код, а якщо в акаунті є двоетапна перевірка, то й пароль. "
        + $"Форма діє {minutes} хв. Не пересилайте код повідомленням.";

    public static string Done(string? account) =>
        $"✅ Сесію оновлено{(account is null ? string.Empty : $" ({TelegramHtml.Escape(account)})")}. Наступний збір прочитає канали з нею.";

    public static string Failed(string? reason) =>
        $"Не вдалося увійти{(reason is null ? string.Empty : $": <code>{TelegramHtml.Escape(reason)}</code>")}. Спробуйте /login ще раз або TgLogin.";
}
