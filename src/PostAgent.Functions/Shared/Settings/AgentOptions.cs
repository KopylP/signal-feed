using System.ComponentModel.DataAnnotations;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Settings;

// Усі App Settings, що читає код. Прив'язуються з кореня конфігурації за іменами властивостей.
// Розклади (CollectorSchedule, DigestSchedule, AlertSchedule, DailySummarySchedule) сюди не входять:
// їх читають атрибути [TimerTrigger("%...%")] безпосередньо з App Settings.
public sealed class AgentOptions
{
    // Скільки записів вміщує одне повідомлення алерта (ліміт Telegram 4096 символів). Запис не перевищує ~450
    // символів (тема 60, пояснення 200, джерело й посилання), тож 8 записів вкладаються.
    public const int MaxItemsPerMessage = 8;

    // Дайджест ділиться на кілька повідомлень, тож його розмір не обмежений одним повідомленням,
    // а лише тим, скільки постів модель встигає осмислено підсумувати (ADR-0009).
    public const int MaxDigestItems = 12;

    // Джерела
    public string Sources { get; set; } = string.Empty;

    // Дайджест
    [Range(1, MaxDigestItems)] public int DigestSize { get; set; } = 7;
    [Range(0, 10)] public int InterestingThreshold { get; set; } = 7;

    // Збір і оцінювання
    [Range(1, 100)] public int ScoringBatchSize { get; set; } = 10;
    [Range(1, 4000)] public int MaxPostChars { get; set; } = 1500;
    [Range(0, 4000)] public int MinPostChars { get; set; } = 80;
    [Range(1, 168)] public int FirstRunLookbackHours { get; set; } = 24;
    [Range(1, 365)] public int RetentionDays { get; set; } = 2;

    // How many days the daily summaries (counts per source and topic, no post text) are kept for trends and /stats.
    [Range(7, 365)] public int StatsRetentionDays { get; set; } = 90;

    // Групи: не більше стільки постів на групу за запуск, решта стискається в пачки
    [Range(1, 50)] public int MaxItemsPerGroup { get; set; } = 5;
    [Range(1, 20000)] public int MaxBundleChars { get; set; } = 3000;

    // Алерти (лише для груп зі списку AlertSources)
    public string AlertSources { get; set; } = string.Empty;
    [Range(1, 24)] public int AlertWindowHours { get; set; } = 3;
    [Range(0, 24)] public int AlertCooldownHours { get; set; } = 3;
    [Range(0, 10)] public int AlertThreshold { get; set; } = 9;
    [Range(1, 1000)] public int BurstMinPosts { get; set; } = 8;
    [Range(1, MaxItemsPerMessage)] public int AlertMaxPerRun { get; set; } = 3;
    public string AlertQuietHours { get; set; } = string.Empty;
    // Only for the quiet hours; the time the agent shows and understands is TimeZone.
    public string AlertTimeZone { get; set; } = "Europe/Kyiv";

    // The reader's time zone (an IANA id): the times in the digest, in the chat and in the date and time of a post search.
    // Storage and the daily partitions stay in UTC.
    public string TimeZone { get; set; } = "Europe/Kyiv";

    // Allows jobs to start from a message in the run-now queue (tools/run-now.cs). Keep it off in Azure except while testing.
    public bool ManualRunsEnabled { get; set; }

    // OpenAI
    public string OpenAIModel { get; set; } = "gpt-5.4-nano";
    [Required] public string OpenAIApiKey { get; set; } = string.Empty;

    // Chat about the digest: a stronger model than the one that scores posts, and the bounds of one conversation.
    public string OpenAIChatModel { get; set; } = "gpt-6.1-terra";
    [Range(1, 100)] public int ChatMaxMessages { get; set; } = 20;
    // Hidden reasoning tokens count against this limit, so it is well above the length of a Telegram answer.
    [Range(1, 32000)] public int ChatMaxOutputTokens { get; set; } = 4000;
    // None, Low, Medium or High (case does not matter). The chat uses the Responses API, the only one that allows tools together with reasoning.
    [RegularExpression("(?i)^(none|low|medium|high)$")] public string ChatReasoningEffort { get; set; } = "medium";
    [Range(1, 20)] public int ChatMaxToolCalls { get; set; } = 5;
    [Range(1, 200)] public int ChatSearchMaxHits { get; set; } = 8;
    // Shows the answer as it is written (a Telegram message draft). Off: the answer arrives at once, as before.
    public bool ChatStreamEnabled { get; set; } = true;

    // After the digest the reader can rate its entries with 👍 and 👎, and the scoring takes the recent votes into account. Off: no buttons and no votes in scoring.
    public bool FeedbackEnabled { get; set; } = true;

    // Telegram
    [Required] public string TelegramBotToken { get; set; } = string.Empty;
    // Необов'язковий: якщо не задано, чат береться з прив'язки (/start <TelegramPairingKey>).
    public long TelegramChatId { get; set; }
    [Required] public string TelegramPairingKey { get; set; } = string.Empty;
    // How many chats can be paired at once (ADR-0014). Every bound chat gets the digest and alerts.
    [Range(1, 20)] public int TelegramMaxChats { get; set; } = 5;
    [Required] public string TelegramWebhookSecret { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int TelegramApiId { get; set; }
    [Required] public string TelegramApiHash { get; set; } = string.Empty;
    public string SessionBlobContainer { get; set; } = StorageNames.DefaultSessionContainer;

    // Set by the Azure platform, never by us: the host name the Mini App of /login is served from (ADR-0016). Empty locally.
    [ConfigurationKeyName("WEBSITE_HOSTNAME")] public string WebsiteHostName { get; set; } = string.Empty;

    // A shallow copy is enough: every property is a value or an immutable string.
    public AgentOptions Clone() => (AgentOptions)MemberwiseClone();

    public IReadOnlyList<string> SourceList => SourceNames.Parse(Sources);
    public IReadOnlySet<string> AlertSourceSet => SourceNames.Parse(AlertSources).ToHashSet();
}
