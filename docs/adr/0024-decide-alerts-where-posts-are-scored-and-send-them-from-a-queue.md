# Decide alerts where posts are scored and send them from a queue

- Status: Accepted
- Date: 2026-11-28
- Deciders: Maintainer

## Context

Alerts ran on an hourly timer (`AlertJob`, `AlertSchedule`). A post was collected within the hour and scored within minutes, then waited up to another hour
for the timer, so an "instant" alert came up to two hours late. The timer job also selected the posts, embedded the followed topics through OpenAI on every
run and sent the message, mixing three jobs; the embedding of posts lived elsewhere (ADR-0023). The owner set a rule for the pipeline: a stage publishes to a
queue only the messages its consumer will really serve, so the decision belongs to the producer and the consumer only acts.
The owner also chose (2026-11-28): no alert is published during quiet hours (those posts go to the digest), collection stays hourly, all three kinds of alert
stay (an important group post, a burst in a group, a followed topic), the message format stays, near-duplicates are looked for among posts published within
3 hours of each other, and the stage and queue names say what they do.

## Decision

- **The scorer decides.** After a batch is scored and saved, `AlertDecider` chooses, under the lease of `settings/locks/alert-decision.lock` (one decision at a
  time, so the cooldown of a group holds across parallel batches): the important or bursting posts of the groups in `AlertSources` (threshold, window, cooldown,
  at most `AlertMaxPerRun`) and the posts that match a followed topic (the post's stored vector against the topic's stored vector, or the words), at most 5.
  It marks them (`AlertedAt` for a group alert, which starts the cooldown; `WatchAlertedAt` for a topic) and publishes them to the queue `to-alert`.
  During quiet hours it publishes nothing.
- **The sender only sends.** `AlertSenderFunction` (queue `to-alert`) formats the message, sends it and marks the posts `Sent`. The timer, `AlertJob` and
  `AlertSchedule` are removed.
- **Topics are embedded once, by their own stage.** When a change of the settings adds a topic (`SettingsEditor` compares the topics before and after the save),
  the phrase is published to `topics-to-follow`. `FollowTopicFunction` (`TopicFollower`) embeds it if it has no vector yet, stores its vector in
  `settings/followed-topics.json` (vectors of removed topics stay, unused, so two topics added at once never drop each other), and lets `AlertDecider` announce the matching posts of the last 26 hours. Nothing else calls OpenAI for topics.
- **The deduplication stage is named for its work.** `EmbeddingFunction` becomes `DeduplicationFunction` (`PostDeduplicator`), its queue `posts-collected`
  becomes `to-deduplicate` and its lock `deduplication.lock`. A post is compared with the single posts published within `DuplicateWindowHours` (3) of it.

## Options considered

- **Keep the timer and only move topic embedding out**: least change, but alerts stay up to two hours late and the job keeps choosing what to send.
- **Publish every scored post to an alert queue and let the consumer filter**: simple producer, but breaks the rule (most messages would be rejected).
- **Decide in the scorer, send from a queue (chosen)**: the alert comes minutes after scoring, each stage has one job, and the queue carries real work only.
- **Decide without a lock**: less code, but two batches of one group scored at once would both pass the cooldown.
- **Embed topics in the alert decision**: no new stage, but every decision would call OpenAI and depend on it.

## Consequences

An alert now comes within about an hour of publication (the collection interval) instead of two. A post that matched during quiet hours, or beyond the 5 per
decision, is not announced later: it goes to the digest. A topic set only as the `Watches` App Setting, never through the bot, has no vector until it is added
through the bot (it matches by words meanwhile). If publishing to `topics-to-follow` fails after the settings were saved, the topic matches by words only until
it is added again. A decided alert whose send fails keeps its mark, so it is not decided again; after the queue gives up it lands in `to-alert-poison` and the
post goes to the digest. The rename needs the new queues first and the old queue removed only after the deploy. ADR-0023 still holds for the vectors and the
duplicate rule; its window of 26 hours is replaced by `DuplicateWindowHours`.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
