---
name: write-adr
description: Use when a significant or hard-to-reverse architectural decision is made, discussed or reversed (hosting, storage model, identity and access, deployment, pipeline shape, testing approach), or when asked to record a decision or explain why something was chosen.
---

# Write an ADR

ADRs live in `docs/adr/`. Read `docs/adr/README.md` first: it holds the rules and the index. An ADR records why we chose something,
so the next maintainer (or assistant) does not have to rediscover or re-argue it.

## Decide whether it deserves an ADR

Write one when the decision is hard to reverse or shapes other work, and a future reader would ask "why is it done like this?".
Do not write one for a naming choice, a small refactor, a temporary workaround, or something already dictated by a standard.
If unsure, ask the user in one line instead of writing a file nobody wanted.

## Create a new ADR

1. List `docs/adr/` and take the next number (highest `NNNN` + 1, zero-padded to four digits).
2. Copy `docs/adr/template.md` to `docs/adr/NNNN-imperative-title.md`. The title is a short imperative phrase in lowercase with dashes
   (`choose-hosting-plan`, `keep-secret-values-out-of-terraform-state`).
3. Fill every section from facts in the conversation and the code, not from guesses:
   - **Context**: the situation and constraints, with dates for anything time-sensitive.
   - **Decision**: one or two sentences, active voice ("We use ...").
   - **Options considered**: the real alternatives with pros and cons. Include the option that was rejected and why.
   - **Consequences**: what gets easier, what gets harder, follow-up work, and when to revisit.
   Reference code by path instead of pasting it. Keep it to about one page.
4. Set `Status: Proposed` while it is under discussion and `Accepted` once the user agrees. Use today's date.
5. Add a row to the index table in `docs/adr/README.md` in the same change.
6. Write the ADR in English, like all project documentation.

## Change or reverse an earlier decision

Never rewrite the Decision of an `Accepted` ADR: the record is only useful if it shows what was true at the time.
Instead:
1. Write a new ADR that explains the new decision and links the old one in its Context.
2. In the old ADR, change only the status line to `Superseded by ADR-NNNN` and add a dated line under `Updates`.
3. Update both rows in the index.

Small clarifications (a new fact, a follow-up that was done) go under `Updates` as a dated note, leaving the other sections intact.

## Finish

Mention the new ADR in your summary and commit it together with the change it explains, so the code and its reasoning stay in one commit.
