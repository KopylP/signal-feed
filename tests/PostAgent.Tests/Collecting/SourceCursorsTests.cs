using Azure.Data.Tables;
using PostAgent.Collecting.Infrastructure;
using PostAgent.Shared.Infrastructure;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Collecting;

public class SourceCursorsTests
{
    [Fact]
    public async Task Starts_from_zero_when_the_source_has_no_cursor_yet()
    {
        var cursors = new SourceCursors(new FakeTableServiceClient());

        Assert.Equal(0, await cursors.GetLastPostIdAsync("badseo", CancellationToken.None));
    }

    [Fact]
    public async Task Returns_the_stored_cursor_of_the_source()
    {
        var tables = new FakeTableServiceClient();
        tables.Table(StorageNames.CursorsTable).Add(new TableEntity("source", "badseo") { ["LastPostId"] = 42 });

        Assert.Equal(42, await new SourceCursors(tables).GetLastPostIdAsync("badseo", CancellationToken.None));
    }
}
