namespace PostAgent.Shared.Chat;

// Every command of the bot and what it is for: the one list the agent reads when the reader asks what the bot can do (get_commands).
// A new command is added here, in its handler and in the docs. Descriptions are Ukrainian: they are relayed to the end user.
public static class BotCommands
{
    public static readonly IReadOnlyList<BotCommand> All =
    [
        new("/settings", "відкриває меню налаштувань: пороги, час тихих годин, часовий пояс, джерела, моделі; те саме можна попросити в агента словами"),
        new("/digest [період]", "дайджест за будь-який період поза розкладом: /digest (остання доба), /digest 6 (6 годин), /digest today або /digest вчора, /digest 2026-11-25T18:00 2026-11-25T23:00; відповідь приходить лише вам"),
        new("/stats [днів]", "статистика за 7 днів (або /stats 30): скільки постів зібрано й оцінено, найкорисніші та найслабші джерела, теми, токени моделей"),
        new("/cancel", "скасувати питання, яке бот чекає від вас у меню налаштувань"),
        new("/clear", "забути попередні питання й відповіді розмови (дайджест лишається)"),
        new("/login", "увійти в Telegram-акаунт, з якого читаються канали (оновити сесію) через форму в боті"),
        new("/start <ключ>", "прив'язати цей чат до бота ключем з налаштувань; потрібен один раз при встановленні"),
    ];

    public const string Buttons =
        "Під кожним дайджестом є кнопки 👍/👎 для оцінки записів: вони підлаштовують наступне оцінювання під ваш смак. "
        + "Усе інше можна просто писати агентові як питання: про дайджест, пости, тренди за тижні, зміну налаштувань, джерел і промптів.";
}
