using Azure;

namespace PostAgent.Shared.Infrastructure;

// A blob as it was read: its bytes and the version (ETag) a conditional write or delete must match.
public sealed record BlobContent(BinaryData Data, ETag ETag, DateTimeOffset LastModified)
{
    public string Text => Data.ToString();
}
