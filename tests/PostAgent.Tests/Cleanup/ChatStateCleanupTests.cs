using PostAgent.Cleanup;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Cleanup;

public class ChatStateCleanupTests
{
    private static async Task<(ChatStateCleanup Task, InMemoryChatStateStore Store)> Create(DateTimeOffset reportCreatedAt, int retentionDays = 1)
    {
        var store = new InMemoryChatStateStore();
        await store.StartNewAsync(new ReportSnapshot("r", reportCreatedAt, null, [], []), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new AgentOptions { RetentionDays = retentionDays });
        return (new ChatStateCleanup(store, options), store);
    }

    [Fact]
    public async Task Deletes_a_conversation_whose_digest_is_older_than_the_retention()
    {
        var (task, store) = await Create(Now.AddDays(-2));

        var deleted = await task.RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Null(store.Current);
    }

    [Fact]
    public async Task Keeps_a_fresh_conversation()
    {
        var (task, store) = await Create(Now.AddHours(-3));

        var deleted = await task.RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.NotNull(store.Current);
    }

    [Fact]
    public async Task The_retention_setting_moves_the_cutoff()
    {
        var (task, store) = await Create(Now.AddDays(-2), retentionDays: 3);

        Assert.Equal(0, await task.RunAsync(Now, CancellationToken.None));
        Assert.NotNull(store.Current);
    }

    [Fact]
    public async Task Is_named_for_the_log()
    {
        var (task, _) = await Create(Now);

        Assert.Equal("ChatState", task.Name);
    }
}
