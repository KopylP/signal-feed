namespace PostAgent.Selection;

/// <summary>Застосовує оцінки моделі до постів.</summary>
public static class ScoreApplier
{
    /// <summary>Записує оцінки в пости. Повертає id постів, для яких оцінки немає.</summary>
    public static IReadOnlyList<string> Apply(IEnumerable<PostEntity> posts, IReadOnlyList<PostScore> scores)
    {
        var batch = posts.ToList();
        var batchIds = batch.Select(p => p.RowKey).ToHashSet();
        var byId = scores.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

        var missing = new List<string>();
        foreach (var post in batch)
        {
            if (byId.TryGetValue(post.RowKey, out var score))
            {
                post.ApplyScore(score, batchIds);
            }
            else
            {
                missing.Add(post.RowKey);
            }
        }

        return missing;
    }
}
