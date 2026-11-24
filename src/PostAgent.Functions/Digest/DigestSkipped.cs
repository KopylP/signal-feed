namespace PostAgent.Digest;

// Posts that are left out of the digest: near-duplicates, and everything else that was not shown.
public sealed record DigestSkipped(int Duplicates, int Other)
{
    public static readonly DigestSkipped None = new(0, 0);

    public int Total => Duplicates + Other;

    public DigestSkipped Add(DigestSkipped more) => new(Duplicates + more.Duplicates, Other + more.Other);
}
