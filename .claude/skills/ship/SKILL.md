---
name: ship
description: Use when asked to ship, release, deploy, merge to main or roll back a finished change.
disable-model-invocation: true
---

# Ship

## Overview

A push to `main` that changes code is deployed and released as `vYYYY.MM.DD.<run_number>` (ADR-0005); the number appears only after the run.
Merging and deploying are hard to undo, so confirm with the user before every merge and every rollback. Never create tags or releases by hand: the pipeline does it,
and there is no manual release path. Infra apply and deploy work only from `main` (the OIDC credential accepts nothing else).

## Before merging

1. **REQUIRED SUB-SKILL:** use `check-before-pr` and resolve everything it reports.
2. Deploy order: if the change needs a new required App Setting, Terraform variable or secret, split it. Merge the infra part first (`infra/**` runs no pipeline), then run the manual
   `infra` workflow from `main` (plan, read it for destroy/replace, apply), set Key Vault secrets, and ask the owner to confirm the setting exists
   (`az functionapp config appsettings list -g <rg> -n <app> --query "[].name"`). Only then merge the code. A single PR with both deploys the code first and the app fails to start.
3. `gh variable get DEPLOY_ENABLED`. If it is not `true`, the merge builds and tests but neither deploys nor releases; tell the user.
4. Confirm the owner's topics in `prompts/scoring.md` are filled in before the first deploy.

## Merge

Open a pull request (title written as a release note), wait for the `build-test` check, then squash merge once the user confirms.
The pipeline is skipped only when every changed file is under `docs/**`, `README.md`, a `CLAUDE.md`, `.claude/**` or `infra/**`; any other file in the diff runs it.
Asked to "release" a change that skips it: explain that a release exists only for code changes, and merge it without one.

## Verify

`gh run watch` the run, then `gh release view v<version>`. Check that the tag points at the merged commit and Application Insights shows no startup errors (see `triage-deploy-failure` if it does).

## Roll back

Confirm with the user first, and check `DEPLOY_ENABLED` is `true` (otherwise nothing deploys).

1. Stop the damage: `gh workflow run deploy.yml -f tag=<previous release tag>`. It rebuilds that tag, deploys it and creates no release.
2. Check the old version can start: `git diff <tag> main -- src/PostAgent.Functions/Shared/Settings/AgentOptions.cs`. A required setting the old code needs but that was renamed or removed will stop it.
3. Fix `main`: `git revert` the bad merge through a pull request (deploys the old code as a new release). Until then the next merge redeploys the broken code.

A rollback restores code only. Infrastructure, App Settings, secrets and data stay as they are.
