using Azure;
using Azure.Data.Tables;

namespace PostAgent.Tests.Fakes;

// An in-memory table that answers GetEntityIfExists the way the real service does: a missing row gives a response whose
// Value throws (Azure.NoValueResponse), it does not give null.
public sealed class FakeTableClient : TableClient
{
    private readonly Dictionary<(string Partition, string Row), TableEntity> _rows = [];

    public void Add(TableEntity entity) => _rows[(entity.PartitionKey, entity.RowKey)] = entity;

    public override Task<NullableResponse<T>> GetEntityIfExistsAsync<T>(
        string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        NullableResponse<T> response = _rows.TryGetValue((partitionKey, rowKey), out var row)
            ? new Found<T>((T)(object)row)
            : new Missing<T>();
        return Task.FromResult(response);
    }

    private sealed class Found<T>(T value) : NullableResponse<T>
    {
        public override bool HasValue => true;
        public override T Value => value;
        public override Response GetRawResponse() => throw new NotSupportedException();
    }

    private sealed class Missing<T> : NullableResponse<T>
    {
        public override bool HasValue => false;
        public override T Value => throw new InvalidOperationException("Status: 404, Service returned no content");
        public override Response GetRawResponse() => throw new NotSupportedException();
    }
}
