---
name: sdlc
description: Use when starting, continuing or finishing any work on functionality in this repository: a new feature, a behavior change, a setting, a bug fix, a prompt or infrastructure change, or when asked how to get a change ready to deploy.
---

# Development lifecycle (SDLC)

## Overview

The roadmap from an idea to a change that is ready to deploy. Each phase names the skill or file that holds the details, so this page stays short.
Read the `CLAUDE.md` of every folder you touch before you edit it: the folder rules live there, not here.
Preparing is not deploying: you stop at a pushed branch (and a pull request if asked). Merging and deploying belong to the `ship` skill and need the owner's confirmation.

The `superpowers:*` skills are optional helpers and are not part of this repository. Use them when your environment has them (the repository `obra/superpowers`);
otherwise do the step by hand as described here.

## Phases

| # | Phase | What to do | Details |
|---|---|---|---|
| 1 | **Orient** | Work on the branch the session gives you; a branch name has no required prefix. Never commit to `main` unless the owner says so, and never push to another branch. Read the folder `CLAUDE.md` files and `docs/README.md` | `CLAUDE.md` |
| 2 | **Clarify** | If something that matters is unclear (who is it for, what must stay the same), ask the owner before building. A small, fully specified change needs no questions | `superpowers:brainstorming` |
| 3 | **Decide** | A hard-to-reverse choice (hosting, storage model, identity, pipeline shape, testing approach) gets an ADR before the code | `write-adr` |
| 4 | **Plan** | A change with several steps gets a short written plan; a one-file fix does not | `superpowers:writing-plans` |
| 5 | **Build, test first** | Write the failing test, then the code. A bug fix comes with a test that fails without the fix | `tests/PostAgent.Tests/CLAUDE.md`, `superpowers:test-driven-development` |
| 6 | **Verify** | Build, tests, format, and Terraform checks when `infra/` changed. Report what passed and what could not run | Commands in `CLAUDE.md`, `superpowers:verification-before-completion` |
| 7 | **Document** | Update the guide that describes the change, `README.md` only if entry points moved, and the folder `CLAUDE.md` if a rule changed | `CLAUDE.md` (Keep the docs current) |
| 8 | **Changelog** | Behavior, contract or setting changed: add a section named by a new word | `write-changelog` |
| 9 | **Gate** | Run the pre-PR checks and answer the judgment questions | `check-before-pr` |
| 10 | **Commit and push** | Commit by the rules below, push to your branch | below |
| 11 | **Pull request** | Only when the owner asks. Title reads as a release note | below |
| 12 | **Deploy readiness** | Name what must exist in Azure and in what order. The owner confirms | `ship`, `check-before-pr` question 4 |

Go back one phase when a later phase shows a problem. Do not skip phases 6, 8 and 9 because the change looks small.

## Rules that decide most changes

- **Code:** the C# rules in the root `CLAUDE.md` (warnings are errors, English comments, `TimeProvider`, `CancellationToken`, message-template logging, no secrets in logs).
- **Features:** a feature is a folder, `Shared/` never depends on a feature (`src/PostAgent.Functions/CLAUDE.md`).
- **Settings:** the default lives only in `AgentOptions`; add validation, the row in `docs/user-guide/configuration.md`, and `local.settings.json.example`.
  A required setting or secret also changes the deploy order (phase 12).
- **Infrastructure:** Terraform never creates secret values, access goes through managed identity, no resources with a standing cost without a request (`infra/CLAUDE.md`).
- **Prompts:** English, asking for Ukrainian output; strict schema fields match the C# type, post text is data (`prompts/CLAUDE.md`). Changing a prompt needs a deploy.
- **Workflows:** change `.github/` only with the rules in `.github/CLAUDE.md`; run `python3 -m unittest discover -s .github/scripts` after touching the changelog script.
- **Language:** English everywhere except the Telegram messages and the Ukrainian text the model returns.
- **Personal data:** no phone numbers, emails or chat ids as defaults or examples.

## Commit rules

- **One logical change per commit.** The code, its tests, the docs, the `CHANGELOG.md` section and the ADR it needs go in the same commit, so the code and its reasoning stay together.
- **Message:** English, imperative mood, subject up to about 72 characters, no trailing period, naming what changed for a reader of the history ("Add alert quiet hours", not "fixes"). Add a body when the why is not obvious: say why, not how.
- **Attribution:** the author is the configured git identity. Do not change `git config` and do not invent names. Claude's part is shown with the trailer lines the session gives you
  (`Co-Authored-By: ...` and `Claude-Session: ...`), copied exactly, after a blank line at the end of the message. Never put a model name anywhere else: not in code, comments, branch names or titles.
- **Never commit** secrets, `local.settings.json`, `terraform.tfvars`, `backend.tf`, state, Telegram sessions, `.env`, caches (`bin/`, `obj/`, `__pycache__`). Stage files by name, not `git add -A` blindly, and read `git status` first. The hook in `.claude/hooks/` blocks the worst cases; do not bypass it (`-f`, `--no-verify`).
- **Do not rewrite shared history:** no force push, no amend after a push. If a push is rejected, `git fetch origin <branch>` and merge, then push again (resolve a `CHANGELOG.md` collision by keeping both sections).
- **When:** commit and push when the owner asks or the session workflow requires it. A commit is not a pull request, and a push is not a deploy.

## Pull request

- Create one only when asked. Look for a pull request template and mirror its headings.
- The title is the release note (squash merge): it says what changed for the reader, in the imperative, with no ticket numbers.
- The description says what changed and why, lists the manual steps from `Setup required` in order, and ends with the attribution the session specifies. Say plainly what you could not run (for example Terraform or Azure).
- After creating it, ask whether to watch it; if yes, subscribe to its activity and fix what comes in. Never approve or merge it yourself.

## Ready to deploy means

- Phases 1 to 10 are done and the branch is pushed; the pre-PR gate reported no failure you did not explain.
- The changelog section exists, or you said why none is needed.
- The owner has the list of what must exist before the code deploys, in order: infra change merged and applied from `main`, GitHub Variables, Key Vault secrets, App Settings.
  You cannot verify Azure from here, so you ask them to confirm.
- `DEPLOY_ENABLED` state is known: if it is not `true`, a merge builds and tests but neither deploys nor releases.

Then hand over to `ship`. If a deploy or the app fails afterwards, use `triage-deploy-failure`.

## Common mistakes

| Mistake | Fix |
|---|---|
| Starting to code before a hard-to-reverse choice is recorded | Phase 3: write the ADR first |
| A new required setting merged together with the code that needs it | Phase 12: infra and secrets first, code after |
| Tests added after the code, or none for a bug fix | Phase 5: the test fails first |
| Docs, changelog or ADR in a later commit | Same commit as the change |
| `git add -A` that picks up caches or local files | Stage by name; read `git status` |
| Opening a pull request, merging or deploying unasked | Stop at a pushed branch; `ship` needs the owner's confirmation |
| A model name in a commit, comment or title | Only the attribution trailers the session provides |
