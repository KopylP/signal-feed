---
name: ship
description: Use when asked to ship, release, deploy, merge to main or roll back a finished change.
disable-model-invocation: true
---

# Ship

## Overview

A push to `main` that changes code is deployed and released as `vYYYY.MM.DD.<run_number>` (ADR-0005). Merging is hard to undo, so confirm with the user before every merge.
Never create tags or releases by hand: the pipeline does it.

## Before merging

1. **REQUIRED SUB-SKILL:** use `check-before-pr` and resolve everything it reports.
2. Deploy order: a new required App Setting, Terraform variable or secret must already be applied or set. Run the manual `infra` workflow (plan, read it, then apply)
   and set any Key Vault secret first. Infra-only merges deploy nothing.
3. `gh variable get DEPLOY_ENABLED`. If it is not `true`, the merge builds and tests but neither deploys nor releases; tell the user.
4. Prompt files change behavior and ship with the build; confirm the owner's topics in `prompts/scoring.md` are filled in before the first deploy.

## Merge

Open a pull request (title written as a release note), wait for the `build-test` check, then squash merge once the user confirms.
Merges that only touch `docs/**`, `README.md`, `CLAUDE.md` files, `.claude/**` or `infra/**` do not run the pipeline and create no release.

## Verify

`gh run watch` the run, then `gh release view v<version>`. Check that the tag points at the merged commit and that Application Insights shows the new version
without startup errors (see `triage-deploy-failure` if it does).

## Roll back

- Fast: Actions → build-test-deploy → Run workflow with `tag` set to the previous release (for example `v2026.11.20.120`). It rebuilds and deploys that tag and creates no release.
- Clean history: `git revert` the merge commit through a pull request, which deploys the old code as a new release.
- A rollback restores code only. Infrastructure, App Settings, secrets and data stay as they are, and a removed or renamed required setting can stop the old version from starting.
