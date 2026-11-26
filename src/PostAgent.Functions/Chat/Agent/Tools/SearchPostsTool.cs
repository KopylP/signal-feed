using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Common;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

// The hits are capped (ChatSearchMaxHits) and every post is inside a <post> block. The period is the publication time in the reader's time zone (TimeZone).
public sealed class SearchPostsTool(IPostRepository posts, IEffectiveOptions options, TimeProvider time) : IChatTool
{
    private const int ExcerptChars = 300;

    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (
            [Description("Words to look for, for example: google core update. Optional when a period is given.")] string? query = null,
            [Description("How many days back to search, at least 1. Ignored when 'from' is given. Default 1.")] int? days = null,
            [Description("Start of the period in the reader's time zone: a date (2026-11-25) or a date with time (2026-11-25T09:30).")] string? from = null,
            [Description("End of the period in the reader's time zone: a date (2026-11-25, the whole day) or a date with time (2026-11-25T18:00). Default now.")] string? to = null,
            CancellationToken ct = default) => RunAsync(query, days ?? 0, from, to, ct),
        new AIFunctionFactoryOptions
        {
            Name = "search_posts",
            Description = "Search the stored posts for the wider picture, by words, by period (date and time of publication, in the reader's time zone) or both. "
                + "Every word of the query must appear in the post. Returns a few short hits with ids, newest first.",
        });

    private async Task<string> RunAsync(string? query, int days, string? from, string? to, CancellationToken ct)
    {
        var terms = (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var hasPeriod = !string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to);
        if (terms.Length == 0 && !hasPeriod)
        {
            return "Give words to look for or a period (from, to).";
        }

        var settings = await options.GetAsync(ct);
        var zone = LocalTime.Zone(settings.TimeZone);
        var now = time.GetUtcNow();
        if (!PostPeriod.TryCreate(from, to, days, now, settings.RetentionDays, zone, out var window, out var error))
        {
            return error;
        }

        var found = await posts.GetSinceAsync(window.Since, window.Until, ct);

        var hits = found
            .Where(p => p.PublishedAt >= window.Since && p.PublishedAt < window.Until)
            .Where(p => terms.All(t => p.Text.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (p.Topic?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderByDescending(p => p.PublishedAt)
            .Take(settings.ChatSearchMaxHits)
            .ToList();

        var header = $"Period: {window.Describe(zone)}.";
        return hits.Count == 0
            ? header + " No posts found."
            : header + "\n\n" + string.Join("\n\n", hits.Select(p => DataBlock.Wrap("post", PostDescription.Of(p, zone) + "\n" + p.Text.Truncate(ExcerptChars, "…"))));
    }
}
