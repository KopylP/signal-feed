# Embed posts before scoring to drop duplicates and match followed topics

- Status: Accepted
- Date: 2026-11-27
- Deciders: Maintainer

## Context

A followed topic matched only when every word of the phrase was in the post (`WatchMatcher`): «google core update» missed «Google оновив основний алгоритм»,
and a Ukrainian phrase missed an English post. Near-duplicates were found only inside one scoring batch (`duplicate_of`), so the same news from two channels,
or from two runs, was scored twice and could reach the digest twice. Collection queued posts straight to `to-score`. The budget is $5 a month and no resource
with a standing cost may be added (`infra/CLAUDE.md`).

## Decision

We add an embedding stage to the pipeline: `CollectorFunction` → queue `posts-collected` → `EmbeddingFunction` → queue `to-score` → `ScorerFunction`.
The stage (`Embedding/PostEmbedder`) embeds the new posts with `text-embedding-3-small` cut to 512 dimensions (`PostVectors`) and stores each vector on its row of `Posts`
(`Embedding`, 2 KB). A single message whose vector is at least `DuplicateSimilarityThreshold` (0.9) alike to an earlier single message of the last 26 hours gets
`DuplicateOf` and the status `Scored` without a score, and is never sent to the model. The alert job embeds the followed topics on every run and a post matches
a topic when its vector is at least `WatchSimilarityThreshold` (0.45) alike, or when the words match as before. Bundles of group messages are not deduplicated.
Group bundling stays inside the collector.

## Options considered

- **Ask the scoring model to tag the followed topics**: no new stage and no extra call, but a topic added later never matches the posts already scored,
  and duplicates across batches stay unsolved.
- **A separate aggregation stage for group bundles**: bundling needs every message of one run of a group, a queue message is capped at 64 KB, and the stage
  would write the raw messages and then the bundles; nothing gained.
- **Store topic vectors with the settings**: saves one small call an hour, but needs a write path and invalidation on every topic change.
  Embedding the topics in each alert run makes a new topic match the last day at once.
- **A vector index (Azure AI Search, Cosmos DB)**: real search, but a standing cost; a few hundred posts a day are compared in memory.
- **Embedding stage before scoring (chosen)**: one cheap call per batch, duplicates cost no scoring tokens, and the stored vectors serve later features.

## Consequences

Scoring no longer depends on the embedding call: when it fails, the posts go on to `to-score` without vectors (no deduplication, topics by words only).
The thresholds are App Settings, to be tuned on real posts; 1 turns a check almost off. A duplicate has no score, so it is counted among the skipped posts of the digest
and as a duplicate in the daily summary, but not as scored. The new queue must exist before the code deploys (Terraform, `infra/storage.tf`).
Changing the embedding model makes the stored vectors incomparable until they age out (`RetentionDays`); it is a constant in `PostVectors` for that reason.
Embedding calls are not counted in `/stats` (cents a month). Revisit when the vectors are wanted for search in the chat, or the volume outgrows in-memory comparison.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
