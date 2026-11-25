---
name: check-before-pr
description: Use when a change is finished and about to be committed, pushed or opened as a pull request, or when asked "is this ready", "check my changes" or "finish up".
---

# Check before PR

## Overview

A merge to `main` deploys and releases, so the gate runs before the pull request, not after. A script covers what is mechanical;
the rest needs judgment about the diff and cannot be scripted.

## Mechanical checks

Run `bash .claude/skills/check-before-pr/scripts/check.sh` from the repository root, even for docs-only changes (it is fast).
It builds with warnings as errors, runs the tests, runs `terraform fmt -check` and `validate` when `infra/` changed, and fails if a forbidden file
(secrets, state, local settings, Telegram sessions, `.env`) is in the diff. Report which checks passed and which were not run.
Do not run `terraform plan` or `apply`: they need Azure, and applying is the manual `infra` workflow.

## Judgment checks

Read the whole change: `git diff origin/main` (committed, staged and unstaged) and `git status --short` for untracked files. Answer each:

1. **Docs current?** Is all documentation current, as in the checklist of the `sdlc` skill ("Documentation is part of done"): the user and developer guides, diagrams, ADRs, `README.md`, the `CLAUDE.md` of the changed folder (and the root one), examples and the changelog, updated in the same change. A document or diagram that still describes the old behavior blocks the pull request (the diagrams phase of `sdlc`).
2. **ADR?** Is there a hard-to-reverse decision (hosting, storage model, identity, pipeline shape)? Use the `write-adr` skill.
3. **Release impact?** The merge deploys and creates a release unless it only touches `docs/**`, `README.md`, `CHANGELOG.md`, `CLAUDE.md` files, `.claude/**` or `infra/**`.
   State which applies, and suggest a PR title that reads as a release note.
4. **Deploy order?** A required App Setting, Terraform variable or secret must exist in Azure before the code deploys, otherwise the app fails to start.
   Infra can be applied only from `main` (the OIDC credential accepts nothing else), so the infra change merges first (it deploys nothing), then `infra` apply and the secrets, then the code.
   List the exact names the owner must create: Terraform variable, GitHub Variable (`TF_*`), App Setting, Key Vault secret. You cannot verify Azure from here; ask the owner to confirm
   (`az functionapp config appsettings list -g <rg> -n <app> --query "[].name"`).
5. **Personal data?** Read the added lines for phone numbers, emails, chat ids, and look-alike values (long digit strings, `+380...`) used as defaults or examples. Placeholders only.
6. **Changelog?** If the change alters behavior, a contract or a setting, is there a section named by a new word in `CHANGELOG.md` in the same change? Use the `write-changelog` skill.
   Internal-only changes need no entry; say so.
7. **Language?** English everywhere except the Telegram messages and the Ukrainian text the model returns.

## Output

Tell the user plainly: passed, failed, not run, and what they must do before merging, in order. A failed mechanical check or a personal-data hit blocks the PR.
