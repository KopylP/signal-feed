namespace PostAgent.Stats;

public sealed record SourceRank(string Source, int Posts, int Interesting, double AverageScore)
{
    // The share of its scored posts that were interesting: what makes a source worth following.
    public double Share => Posts == 0 ? 0 : (double)Interesting / Posts;
}
