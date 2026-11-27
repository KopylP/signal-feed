using System.Runtime.InteropServices;

namespace PostAgent.Shared.Posts;

// Embeddings of posts and followed topics (ADR-0023): the model's vectors cut to 512 dimensions, stored as raw float bytes (2 KB a post).
public static class PostVectors
{
    // shortcut: a constant, not a setting; vectors of two models cannot be compared, so a change waits until the stored posts age out (RetentionDays).
    public const string Model = "text-embedding-3-small";
    public const int Dimensions = 512;

    // The model reads up to 8191 tokens and a Cyrillic character can cost one, so the text is cut well below that.
    public const int MaxTextChars = 6000;

    public static byte[] ToBytes(ReadOnlySpan<float> vector) => MemoryMarshal.AsBytes(vector).ToArray();

    public static double Similarity(byte[]? a, byte[]? b) => b is null ? 0 : Similarity(a, MemoryMarshal.Cast<byte, float>(b));

    // Cosine similarity; 0 when a side has no vector or the sizes differ.
    public static double Similarity(byte[]? a, ReadOnlySpan<float> b)
    {
        if (a is null)
        {
            return 0;
        }

        var x = MemoryMarshal.Cast<byte, float>(a);
        if (x.Length != b.Length || x.Length == 0)
        {
            return 0;
        }

        double dot = 0, xx = 0, yy = 0;
        for (var i = 0; i < x.Length; i++)
        {
            dot += x[i] * b[i];
            xx += x[i] * x[i];
            yy += b[i] * b[i];
        }

        return xx == 0 || yy == 0 ? 0 : dot / Math.Sqrt(xx * yy);
    }
}
