---
name: check-before-pr
description: Use when a change is finished and about to be committed, pushed or opened as a pull request, or when asked "is this ready", "check my changes" or "finish up".
---

# Check before PR

## Overview

A merge to `main` deploys and releases, so the gate runs before the pull request, not after. A script covers what is mechanical;
the rest needs judgment about the diff and cannot be scripted.

## Mechanical checks

Run `bash .claude/skills/check-before-pr/scripts/check.sh` from the repository root. It builds with warnings as errors, runs the tests,
runs `terraform fmt -check` and `validate` when `infra/` changed, and fails if a forbidden file is in the diff.
Report which checks passed and which were not run. Do not run `terraform plan` or `apply`: they need Azure, and applying is the manual `infra` workflow.

## Judgment checks

Read `git diff origin/main...HEAD` and answer each:

1. **Docs current?** If settings, commands, variables, workflows or folder structure changed, are `README.md` and the matching `CLAUDE.md` updated in the same change?
2. **ADR?** Is there a hard-to-reverse decision (hosting, storage model, identity, pipeline shape)? Use the `write-adr` skill.
3. **Release impact?** The merge deploys and creates a release unless it only touches `docs/**`, `README.md`, `CLAUDE.md` files, `.claude/**` or `infra/**`.
   State which applies, and suggest a PR title that reads as a release note.
4. **Deploy order?** A new required App Setting, Terraform variable or secret must exist in Azure before the code deploys. Name what the owner must set and run first.
5. **Personal data?** Phone, email or chat ids as defaults or examples are not allowed. Placeholders only.
6. **Language?** English everywhere except `prompts/` and Telegram messages.

## Output

Tell the user plainly: passed, failed, not run, and what they must do before merging. A failed mechanical check blocks the PR.
