using Azure.Data.Tables;

namespace PostAgent.Tests.Fakes;

public sealed class FakeTableServiceClient : TableServiceClient
{
    private readonly Dictionary<string, FakeTableClient> _tables = [];

    public FakeTableClient Table(string name)
    {
        if (!_tables.TryGetValue(name, out var table))
        {
            _tables[name] = table = new FakeTableClient();
        }

        return table;
    }

    public override TableClient GetTableClient(string tableName) => Table(tableName);
}
