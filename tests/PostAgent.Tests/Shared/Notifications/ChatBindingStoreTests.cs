using Azure.Data.Tables;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Notifications.Telegram;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Notifications;

public class ChatBindingStoreTests
{
    private readonly FakeTableServiceClient _tables = new();

    private ChatBindingStore Store => new(_tables);

    [Fact]
    public async Task Binds_several_chats_and_lists_them()
    {
        await Store.TryBindAsync(42, 5, CancellationToken.None);
        await Store.TryBindAsync(43, 5, CancellationToken.None);

        Assert.Equal([42L, 43L], await Store.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_same_chat_is_bound_once()
    {
        await Store.TryBindAsync(42, 5, CancellationToken.None);

        Assert.Equal(BindResult.AlreadyBound, await Store.TryBindAsync(42, 5, CancellationToken.None));
        Assert.Equal([42L], await Store.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_chat_beyond_the_limit_is_not_added()
    {
        await Store.TryBindAsync(42, 1, CancellationToken.None);

        Assert.Equal(BindResult.Full, await Store.TryBindAsync(43, 1, CancellationToken.None));
        Assert.Equal([42L], await Store.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_single_row_of_the_earlier_version_still_counts_as_a_chat()
    {
        _tables.Table(StorageNames.ChatBindingTable).Add(new TableEntity("chat", "digest") { ["ChatId"] = 42L });

        Assert.Equal([42L], await Store.GetAllAsync(CancellationToken.None));
        Assert.Equal(BindResult.AlreadyBound, await Store.TryBindAsync(42, 5, CancellationToken.None));
        Assert.Equal(BindResult.Added, await Store.TryBindAsync(43, 5, CancellationToken.None));
        Assert.Equal([42L, 43L], await Store.GetAllAsync(CancellationToken.None));
    }
}
