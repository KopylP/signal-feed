using PostAgent.Digest;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestMessageTests
{
    private const int TelegramLimit = 4096;

    private static DigestReport Report(
        string? overview = "Огляд дня",
        IReadOnlyList<DigestEntry>? headlines = null,
        IReadOnlyList<DigestEntry>? forMarketplace = null,
        IReadOnlyList<DigestEntry>? notable = null,
        IReadOnlyList<DigestEntry>? recommended = null,
        IReadOnlyList<string>? needsVerification = null,
        DigestSkipped? skipped = null) =>
        new(overview, headlines ?? [], forMarketplace ?? [], notable ?? [], recommended ?? [], needsVerification ?? [], skipped ?? DigestSkipped.None);

    private static string Single(DigestReport report) => Assert.Single(DigestMessage.Format(report));

    [Fact]
    public void Shows_the_channel_the_date_and_the_link_from_the_stored_post()
    {
        var post = Post("a", source: "badseo", hoursAgo: 2);
        post.Url = "https://t.me/badseo/77";

        var html = Single(Report(headlines: [new DigestEntry(post, "Заголовок", "Суть новини", "Чому важливо")]));

        Assert.Contains("@badseo", html);
        Assert.Contains("24.11", html);
        Assert.Contains("href=\"https://t.me/badseo/77\"", html);
        Assert.Contains("Суть новини", html);
        Assert.Contains("Чому важливо", html);
    }

    [Fact]
    public void Escapes_model_generated_text_and_the_url()
    {
        var post = Post("a");
        post.Url = "https://t.me/s/1?a=\"q\"&b=2";

        var html = Single(Report(
            overview: "О <b>&</b>",
            headlines: [new DigestEntry(post, "A <i>B</i>", "x < y", "a > b")],
            needsVerification: ["<script>"]));

        Assert.Contains("О &lt;b&gt;&amp;&lt;/b&gt;", html);
        Assert.Contains("A &lt;i&gt;B&lt;/i&gt;", html);
        Assert.Contains("x &lt; y", html);
        Assert.Contains("href=\"https://t.me/s/1?a=&quot;q&quot;&amp;b=2\"", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>&</b>", html);
    }

    [Fact]
    public void Shows_only_the_sections_that_have_content()
    {
        var entry = new DigestEntry(Post("a"), "З", "Текст", "Чому");

        var html = Single(Report(headlines: [entry], recommended: [new DigestEntry(Post("b"), null, "Бо варто", null)]));

        Assert.Contains("🔥 <b>Головне за день</b>", html);
        Assert.Contains("📖 <b>Рекомендую прочитати</b>", html);
        Assert.DoesNotContain("💼", html);
        Assert.DoesNotContain("👀", html);
        Assert.DoesNotContain("⚠️", html);
        Assert.DoesNotContain("📌", html);
    }

    [Fact]
    public void Shows_the_marketplace_notable_and_verification_sections()
    {
        var html = Single(Report(
            forMarketplace: [new DigestEntry(Post("a"), "Для нас", "Нотатка А", null)],
            notable: [new DigestEntry(Post("b"), "Цікаво", "Нотатка Б", null)],
            needsVerification: ["Чи це підтверджено Google?"]));

        Assert.Contains("💼 <b>Корисно для Collaborator</b>", html);
        Assert.Contains("👀 <b>Варто звернути увагу</b>", html);
        Assert.Contains("⚠️ <b>Потребує перевірки</b>", html);
        Assert.Contains("Чи це підтверджено Google?", html);
    }

    [Fact]
    public void Shows_how_many_posts_can_be_skipped_and_why()
    {
        var html = Single(Report(skipped: new DigestSkipped(Duplicates: 4, Other: 8)));

        Assert.Contains("📌 <b>Можна пропустити</b>", html);
        Assert.Contains("12", html);
        Assert.Contains("4", html);
        Assert.Contains("8", html);
    }

    [Fact]
    public void A_day_without_entries_still_sends_the_overview()
    {
        var html = Single(Report(overview: "Сьогодні нічого важливого"));

        Assert.Contains("📰 <b>Дайджест</b>", html);
        Assert.Contains("Сьогодні нічого важливого", html);
    }

    [Fact]
    public void A_long_digest_is_split_into_messages_that_fit_and_nothing_is_lost()
    {
        var entries = Enumerable.Range(0, 12)
            .Select(i => new DigestEntry(Post($"p{i}", source: $"src{i}"), new string('т', 300), new string('с', 2000), new string('ч', 1000)))
            .ToList();

        var messages = DigestMessage.Format(Report(overview: new string('о', 2000), headlines: entries, notable: entries.Take(3).ToList()));

        Assert.True(messages.Count > 1);
        Assert.All(messages, m => Assert.True(m.Length <= TelegramLimit, $"Message is {m.Length} chars"));
        for (var i = 0; i < 12; i++)
        {
            Assert.Contains(messages, m => m.Contains($"@src{i}"));
        }
    }

    [Fact]
    public void A_section_continued_in_the_next_message_repeats_its_header()
    {
        var entries = Enumerable.Range(0, 8)
            .Select(i => new DigestEntry(Post($"p{i}"), "З", new string('с', 600), new string('ч', 300)))
            .ToList();

        var messages = DigestMessage.Format(Report(headlines: entries));

        Assert.True(messages.Count > 1);
        Assert.All(messages.Skip(1), m => Assert.Contains("Головне за день", m));
    }

    [Fact]
    public void Text_that_grows_when_escaped_still_fits_into_a_message()
    {
        var entries = Enumerable.Range(0, 8)
            .Select(i => new DigestEntry(Post($"p{i}"), new string('&', 100), new string('&', 700), new string('<', 350)))
            .ToList();

        var messages = DigestMessage.Format(Report(overview: new string('&', 500), headlines: entries));

        Assert.All(messages, m => Assert.True(m.Length <= TelegramLimit, $"Message is {m.Length} chars"));
        Assert.Contains(messages, m => m.Contains("href="));
    }
}
