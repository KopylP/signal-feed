using Azure;
using Azure.Core;
using Azure.Data.Tables;

namespace PostAgent.Tests.Fakes;

// An in-memory table that answers GetEntityIfExists the way the real service does: a missing row gives a response whose
// Value throws (Azure.NoValueResponse), it does not give null.
public sealed class FakeTableClient : TableClient
{
    private readonly Dictionary<(string Partition, string Row), ITableEntity> _rows = [];

    public void Add(ITableEntity entity) => _rows[(entity.PartitionKey, entity.RowKey)] = entity;

    // The HTTP status an update of a row fails with, by row key.
    public Dictionary<string, int> UpdateFailures { get; } = [];

    public List<(string RowKey, ETag IfMatch)> Updates { get; } = [];

    public override Task<Response> UpdateEntityAsync<T>(
        T entity, ETag ifMatch, TableUpdateMode mode = TableUpdateMode.Merge, CancellationToken cancellationToken = default)
    {
        if (UpdateFailures.TryGetValue(entity.RowKey, out var status))
        {
            throw new RequestFailedException(status, "Update failed");
        }

        Updates.Add((entity.RowKey, ifMatch));
        return Task.FromResult<Response>(new EmptyResponse());
    }

    public IReadOnlyList<string> RowKeys => _rows.Keys.Select(k => k.Row).ToList();

    // The filter is ignored: the tests that query keep every row in the partition they ask for.
    public override AsyncPageable<T> QueryAsync<T>(
        string? filter = null, int? maxPerPage = null, IEnumerable<string>? select = null, CancellationToken cancellationToken = default) =>
        AsyncPageable<T>.FromPages([Page<T>.FromValues(_rows.Values.Cast<T>().ToList(), null, new EmptyResponse())]);

    public override Task<Response> AddEntityAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ITableEntity row = entity;
        if (!_rows.TryAdd((row.PartitionKey, row.RowKey), row))
        {
            throw new RequestFailedException(409, "Entity already exists", "EntityAlreadyExists", null);
        }

        return Task.FromResult<Response>(new EmptyResponse());
    }

    public override Task<Response> DeleteEntityAsync(
        string partitionKey, string rowKey, ETag ifMatch = default, CancellationToken cancellationToken = default)
    {
        _rows.Remove((partitionKey, rowKey));
        return Task.FromResult<Response>(new EmptyResponse());
    }

    private sealed class EmptyResponse : Response
    {
        public override int Status => 204;
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = string.Empty;
        public override void Dispose()
        {
        }

        protected override bool TryGetHeader(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];
    }

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
