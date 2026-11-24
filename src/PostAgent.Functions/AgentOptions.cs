using PostAgent.Telegram;

namespace PostAgent;

/// <summary>
/// Усі App Settings, що читає код. Прив'язуються з кореня конфігурації за іменами властивостей.
/// Розклади (CollectorSchedule, DigestSchedule, AlertSchedule) сюди не входять: їх читають
/// атрибути [TimerTrigger("%...%")] безпосередньо з App Settings.
/// </summary>
public sealed class AgentOptions
{
    // Джерела
    public string Sources { get; set; } = "";

    // Дайджест
    public int DigestSize { get; set; } = 5;
    public int InterestingThreshold { get; set; } = 7;

    // Збір і оцінювання
    public int ScoringBatchSize { get; set; } = 10;
    public int MaxPostChars { get; set; } = 1500;
    public int MinPostChars { get; set; } = 80;
    public int FirstRunLookbackHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 1;

    // Групи: не більше стільки постів на групу за запуск, решта стискається в пачки
    public int MaxItemsPerGroup { get; set; } = 5;
    public int MaxBundleChars { get; set; } = 3000;

    // Алерти (лише для груп зі списку AlertSources)
    public string AlertSources { get; set; } = "";
    public int AlertWindowHours { get; set; } = 3;
    public int AlertCooldownHours { get; set; } = 3;
    public int AlertThreshold { get; set; } = 9;
    public int BurstMinPosts { get; set; } = 8;
    public int AlertMaxPerRun { get; set; } = 3;
    public string AlertQuietHours { get; set; } = "";
    public string AlertTimeZone { get; set; } = "Europe/Kyiv";

    // OpenAI
    public string OpenAIModel { get; set; } = "gpt-5.4-nano";
    public string OpenAIApiKey { get; set; } = "";

    // Telegram
    public string TelegramBotToken { get; set; } = "";
    public string TelegramChatId { get; set; } = "";
    public int TelegramApiId { get; set; }
    public string TelegramApiHash { get; set; } = "";
    public string TelegramPhone { get; set; } = "";
    public string SessionBlobContainer { get; set; } = SessionBlob.DefaultContainer;

    public IReadOnlyList<string> SourceList => ParseUsernames(Sources);
    public IReadOnlySet<string> AlertSourceSet => ParseUsernames(AlertSources).ToHashSet();

    private static IReadOnlyList<string> ParseUsernames(string? csv) =>
        (csv ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeUsername)
            .Distinct()
            .ToList();

    /// <summary>"@Name", "https://t.me/Name" і "Name" дають однаковий "name".</summary>
    private static string NormalizeUsername(string value)
    {
        var v = value.Trim();
        foreach (var prefix in new[] { "https://t.me/", "http://t.me/", "t.me/", "@" })
        {
            if (v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                v = v[prefix.Length..];
            }
        }
        return v.Trim('/').ToLowerInvariant();
    }
}
