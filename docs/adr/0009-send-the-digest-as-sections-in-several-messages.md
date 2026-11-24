# Send the digest as sections in several messages

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

The first digest was one Telegram message: a two-sentence overview and up to five records, each with one 200-character sentence and a link. The layout lived in `DigestMessage`
and the model answered a small schema (`overview`, `items` with `id` and `insight`). The owner wants a working digest she can read in 5-7 minutes: the main news with the point and
why it matters for her work, what is useful for the marketplace, less obvious items, one to three posts worth reading in full, a note about what can be skipped, and open questions to verify.
Telegram allows 4096 characters per message, and the sections with several sentences per post do not always fit.

## Decision

We replace the compact digest with a sectioned one. The model answers a larger strict schema (`Digest/Infrastructure/DigestSummary.cs`): an overview, and lists of entries that
refer to posts by `id` and carry only text. Code adds everything that must not be invented: the channel, the publication date and the link, taken from the stored post.
The model sees the top posts by score (up to `DigestSize`); code counts the rest and writes the "can be skipped" line itself. The digest is split into several Telegram messages
at section or entry boundaries, each below the limit. The old compact format is removed, not kept behind a switch.

## Options considered

- **One message, shorter texts**: always one message, but the model gets hard length limits and the sections become one-liners again. Rejected: it is the format we are leaving.
- **One message, cut the tail**: simple, but the end (often "recommended to read") is lost when the text is long.
- **Several messages (chosen)**: nothing is lost, the cost is that the digest arrives as 2-3 messages.
- **Keep both formats behind a `DigestFormat` setting**: easy rollback, but two renderers, two schemas and twice the tests for a format nobody asked to keep.
- **The model also sees every post of the day**: a better "can be skipped" judgment, but several times the tokens. Rejected for now: a count by reason is enough.

## Consequences

The digest is richer and costs more per run (longer input and output, still a few cents a month). A failed model call falls back to a plain list of the selected posts, as before.
The schema, the prompt (`prompts/digest.md`), `DigestMessage` and `DigestSummarizer` now change together; the prompt must respect the per-field limits that `DigestMessage` cuts at.
`DigestSize` has a new default and range because the limit no longer comes from one message. Alerts keep the single-message layout and `MaxItemsPerMessage`.
Posts the model leaves out of every section are still marked as sent and counted in the skipped line. Revisit if the owner wants other sections or a digest per topic.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
