namespace PostAgent.Digest;

// Posts that are left out of the digest: near-duplicates, and everything else that was not shown.
// The counts come from code; the note is the model's own words about what it left out, so its style follows the prompt.
public sealed record DigestSkipped(int Duplicates, int Other, string? Note = null)
{
    public static readonly DigestSkipped None = new(0, 0);

    public int Total => Duplicates + Other;

    public DigestSkipped Add(DigestSkipped more) => new(Duplicates + more.Duplicates, Other + more.Other, Note ?? more.Note);
}
