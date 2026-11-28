namespace PostAgent.Shared.Settings;

// Which lines a text lost and which it gained, in order, for the owner to review a rewrite. Lines that did not change are left out;
// a run of them between two changes shows as one gap.
public static class LineDiff
{
    public enum Mark
    {
        Removed,
        Added,
        Gap,
    }

    // A rewrite is at most a few hundred lines; above this table size the texts are shown as fully replaced instead of aligned.
    private const int MaxCells = 1_000_000;

    public static IReadOnlyList<(Mark Mark, string Line)> Compare(string before, string after)
    {
        var a = Lines(before);
        var b = Lines(after);
        if ((long)(a.Length + 1) * (b.Length + 1) > MaxCells)
        {
            return [.. a.Select(l => (Mark.Removed, l)), .. b.Select(l => (Mark.Added, l))];
        }

        // common[i, j]: how many lines the tails a[i..] and b[j..] share in order.
        var common = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                common[i, j] = a[i] == b[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var result = new List<(Mark, string)>();
        var gap = false;
        void Add(Mark mark, string line)
        {
            if (gap && result.Count > 0)
            {
                result.Add((Mark.Gap, string.Empty));
            }

            gap = false;
            result.Add((mark, line));
        }

        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y])
            {
                gap = true;
                x++;
                y++;
            }
            else if (y == b.Length || (x < a.Length && common[x + 1, y] >= common[x, y + 1]))
            {
                Add(Mark.Removed, a[x++]);
            }
            else
            {
                Add(Mark.Added, b[y++]);
            }
        }

        return result;
    }

    private static string[] Lines(string text) => text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
