
namespace PostAgent.Shared.Infrastructure;

// Every Azure Storage resource the app uses, in one place. Shared by the features and by TgLogin,
// so that Shared/ never has to reference a feature to create the resources.
public static class StorageNames
{
    public const string PostsTable = "Posts";
    public const string CursorsTable = "ChannelState";
    public const string ChatBindingTable = "ChatBinding";
    // Collected posts on their way to the embedding stage, which drops near-duplicates and passes the rest to ScoringQueue (ADR-0023).
    public const string CollectedQueue = "posts-collected";
    public const string ScoringQueue = "to-score";
    public const string RunNowQueue = "run-now";
    public const string ChatQueue = "chat-messages";
    // /digest: a request for a digest of a period, answered to the chat that asked. Made by Terraform (the scale controller needs it to exist).
    public const string DigestRequestQueue = "digest-requests";
    // "The daily summaries are written": the cleanup starts only from this message. Made by Terraform (the scale controller needs it to exist).
    public const string CleanupRequestQueue = "cleanup-requests";
    public const string ChatStateContainer = "chat-state";
    // One conversation per chat (ADR-0014). The old single blob, "current.json", is only deleted by the cleanup once it is stale.
    public static string ChatStateBlobName(long chatId) => chatId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json";
    public const string SettingsContainer = "settings";
    public const string SettingsBlob = "overrides.json";

    // The reader's likes and dislikes of posts (recorded by the chat agent), shared by every chat; the scoring reads it.
    public const string FeedbackBlob = "feedback.json";

    // The lock of the embedding stage's duplicate check (ADR-0023), in the settings container: one queue message at a time.
    public const string EmbeddingLockBlob = "locks/embedding.lock";

    // Long memory (ADR-0018): one small summary per day, and the tokens the models used. In the settings container, under a prefix.
    public static string DailyStatsBlobName(string day) => $"stats/daily/{day}.json";
    public const string DailyStatsPrefix = "stats/daily/";
    public const string UsageBlob = "stats/usage.json";

    // What the chat agent keeps about one chat's reader (ADR-0022): one Markdown blob per note; a chat never reads another chat's notes.
    public static string ReaderMemoryPrefix(long chatId) => $"reader-memory/{chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)}/";

    // The reader profile of ADR-0015, replaced by the reader memory: only read once to be moved into a note, then deleted.
    public static string LegacyProfileBlobName(long chatId) => $"profile/{chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)}.txt";
    public const string SessionBlob = "wtelegram.session";
    public const string CollectionLockBlob = "collect.lock";
    // The sign-in from the bot that is in progress (ADR-0016); it may hold a login code for a few minutes.
    public const string LoginAttemptBlob = "login-attempt.json";
    public const string LoginQueue = "tg-login";
    public const string DefaultSessionContainer = "tg-session";
}
