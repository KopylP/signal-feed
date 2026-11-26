using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;
using PostAgent.Stats;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Stats;

public class StatsInboxTests
{
    private const long BoundChat = 42;

    private readonly InMemoryDailyStatsStore _store = new();
    private readonly InMemoryUsageStore _usage = new();
    private readonly RecordingNotifier _notifier = new();

    private StatsInbox Create(InMemoryPostRepository? posts = null, long? bound = BoundChat)
    {
        var app = new AgentOptions();
        var resolver = new ChatResolver(Microsoft.Extensions.Options.Options.Create(app),
            bound is { } b ? new InMemoryChatBindingStore(b) : new InMemoryChatBindingStore());
        return new StatsInbox(resolver, _store, _usage, posts ?? new InMemoryPostRepository(), _notifier, new FakeEffectiveOptions(app), new FakeTimeProvider(Now), NullLogger<StatsInbox>.Instance);
    }

    private static Update Incoming(string? text, long chatId = BoundChat, ChatType type = ChatType.Private) =>
        new() { Id = 1, Message = new Message { Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type }, Text = text } };

    private static DailyStats Stored(string day, int collected = 100) => new(day, collected, collected, 10, 0, 5.0, [new("chan", 10, 6.0, 5)], [new("google", 4, 6.0)], []);

    [Fact]
    public async Task Answers_the_asking_chat_with_the_stored_days_and_the_tokens()
    {
        _store.Seed(Stored("2026-11-22"), Stored("2026-11-23"));
        _usage.Seed(new UsageDay("2026-11-23", "chat", 3, 1000, 100));

        var handled = await Create().HandleAsync(Incoming("/stats"), CancellationToken.None);

        Assert.True(handled);
        var (chat, html) = Assert.Single(_notifier.SentTo);
        Assert.Equal(BoundChat, chat);
        Assert.Contains("Постів 200", html);
        Assert.Contains("@chan", html);
        Assert.Contains("чат: 1.0K / 100, 3", html);
    }

    [Fact]
    public async Task A_day_still_in_posts_that_has_no_summary_is_summarized_on_the_spot()
    {
        var posts = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 1), Post("b", score: 3, hoursAgo: 1));

        await Create(posts).HandleAsync(Incoming("/stats"), CancellationToken.None);

        Assert.Contains("Постів 2", Assert.Single(_notifier.Replies));
    }

    [Fact]
    public async Task A_stored_day_wins_over_the_live_one()
    {
        _store.Seed(Stored(PostEntity.PartitionFor(Now), collected: 500));
        var posts = new InMemoryPostRepository(Post("a", score: 9, hoursAgo: 1));

        await Create(posts).HandleAsync(Incoming("/stats"), CancellationToken.None);

        Assert.Contains("Постів 500", Assert.Single(_notifier.Replies));
    }

    [Fact]
    public async Task Says_so_when_there_is_nothing_yet()
    {
        await Create().HandleAsync(Incoming("/stats 3"), CancellationToken.None);

        Assert.Equal(StatsMessage.Empty(3), Assert.Single(_notifier.Replies));
    }

    [Theory]
    [InlineData("/stats 0")]
    [InlineData("/stats 91")]
    [InlineData("/stats abc")]
    [InlineData("/stats -1")]
    public async Task An_unreadable_number_of_days_gets_the_usage(string text)
    {
        var handled = await Create().HandleAsync(Incoming(text), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatsMessage.Usage, Assert.Single(_notifier.Replies));
    }

    [Theory]
    [InlineData("/STATS@my_bot 7")]
    [InlineData("  /stats  ")]
    public async Task The_command_is_found_with_a_bot_name_or_spaces(string text)
    {
        Assert.True(await Create().HandleAsync(Incoming(text), CancellationToken.None));
    }

    [Theory]
    [InlineData("привіт")]
    [InlineData("/statsx")]
    [InlineData("/digest")]
    public async Task Other_texts_are_left_to_the_others(string text) =>
        Assert.False(await Create().HandleAsync(Incoming(text), CancellationToken.None));

    [Theory]
    [InlineData(99L, ChatType.Private)]
    [InlineData(BoundChat, ChatType.Group)]
    public async Task Another_chat_or_a_group_is_left_to_the_others(long chatId, ChatType type)
    {
        Assert.False(await Create().HandleAsync(Incoming("/stats", chatId, type), CancellationToken.None));
        Assert.Empty(_notifier.SentTo);
    }
}
