# Record architecture decisions

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

Decisions about hosting, secrets and deployment were made in conversations and commit messages, so the reasons were easy to lose.
The repository is maintained by one person together with an AI assistant, which makes written rationale more valuable, not less.

## Decision

We keep architecture decision records in `docs/adr/`, one Markdown file per decision, following the rules in `docs/adr/README.md`.

## Options considered

- **No records**: no overhead, but the reasoning disappears and gets re-litigated.
- **Wiki or issues**: separate from the code, drifts out of sync, not visible to the assistant.
- **ADRs in the repository**: versioned with the code, reviewable in the same change, readable by the assistant.

## Consequences

Each significant decision costs a short file. The `write-adr` skill and a rule in the root `CLAUDE.md` keep this cheap.
The first records below were written retroactively on the date above.
