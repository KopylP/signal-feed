# Keep a small summary per day as the long memory

- Status: Accepted
- Date: 2026-11-26
- Deciders: Maintainer

## Context

Posts live for `RetentionDays` (2 by default) and are then deleted by day, so the agent knows nothing older than two days: it cannot answer «що змінилось за місяць у темі X»
and `/stats` could only describe two days. Keeping the posts themselves longer costs storage, grows every scan of the `Posts` table and keeps channel text the app has no use for.
The budget is $5 a month and there must be no new resource with a standing cost (`infra/CLAUDE.md`).

## Decision

When a day is over, the cleanup writes one small summary of it before the posts are deleted: counts (collected, scored, interesting, duplicates, average score), the posts and the
average score per source, the topics with counts, and the five best posts (id, source, topic, score, link), never any post text. It is one blob per day, `settings/stats/daily/<yyyy-MM-dd>.json`,
kept for `StatsRetentionDays` (90 by default). The model token counts of every call are one more blob, `settings/stats/usage.json` (30 days). `DailyStatsTask` runs first in the cleanup,
rewrites the complete days still in `Posts` on every run, also the one day `PostsCleanup` deletes in that same run (so a late scored post is not lost and a day has `RetentionDays` + 1 chances), never replaces a fuller summary with a smaller one, and deletes summaries past their period. The chat agent reads the summaries through
`get_trends` and the owner through `/stats`.

## Options considered

- **Keep posts longer (`RetentionDays` 30 or more)**: no new code, but ties the cost and the scan time to the volume, and keeps all channel text.
- **A new table**: queryable, but needs Terraform and an apply before the code, and a table property holds at most 32K characters of JSON anyway.
- **Summaries in blobs under the existing `settings` container (chosen)**: no new infrastructure, a few KB a day, a hand-readable file per day, and the existing role already covers it.
- **Embeddings or a search index**: free-text questions over history, but a standing cost and a lot of machinery for counts the owner needs.

## Consequences

Trends and the best and weakest sources are available for months, but only as counts and names: a question about the wording of an old post cannot be answered. Topics and titles come from channels
through the model, so everything read back is data (`DataBlock`, escaped in the `/stats` message). A day that was never collected or never scored has no summary. A summary is written after the day ends
(03:00 UTC), so «yesterday» is missing until then; `/stats` summarizes the days still in `Posts` on the spot. Days are UTC dates of publication, like the partitions of `Posts`.
Revisit if the owner wants full-text history or per-post lookups: that needs a store with a standing cost, and a new ADR.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->

2026-11-26: The summary no longer runs inside the cleanup. `DailySummaryJob` (a timer, `DailySummarySchedule`) writes the summaries and then queues a `CleanupRequest`; `CleanupFunction` runs the cleanup tasks only from that request, and `PostsCleanup` never deletes past the last summarized day. A failed summary now holds the deletion back instead of losing a day. The summary also covers every day still in `Posts`, even older than the retention. The sentence above that `DailyStatsTask` "runs first in the cleanup" is replaced by this.
