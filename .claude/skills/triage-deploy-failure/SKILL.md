---
name: triage-deploy-failure
description: Use when a build-test-deploy or infra workflow run failed, the function app does not start after a change, a scheduled digest or alert did not arrive, or messages pile up in a queue.
---

# Triage a deploy or runtime failure

## Overview

Find the failing layer first (pipeline, startup, schedule, queue, external service), then fix that layer only.
Stay read-only until the cause is known. "Running" in the portal says nothing about whether the timers succeed.

## Pipeline failed

- `gh run list --workflow deploy.yml --limit 5`, then `gh run view <run-id> --log-failed`. Read the first failing step, not the last error.
- Build or tests: fix in code. Deploy step: check the `AZURE_*` variables and the pipeline identity's roles (ADR-0004). Azure sign-in fails outside `main`: the credential accepts only `refs/heads/main`.
- Release step (403, "Resource not accessible by integration"): the build and deploy already succeeded, so no rollback. Settings → Actions → Workflow permissions must allow `contents: write`, and a tag ruleset must not block `v*`. Re-run only the failed job.
- `infra` run failed: read the plan in the run summary; a state lock means another `infra` run is active.
- Which version is live: the last successful `deploy` job and the latest release tag.

## App does not start

Settings are validated on startup (`ValidateOnStart`), and a bad one stops the app with the parameter name in the log. In Application Insights:

```
traces | where timestamp > ago(2h) and severityLevel >= 3 | order by timestamp desc | take 50
```

What changed: `git log -p -5 -- src/PostAgent.Functions/Shared/Settings infra` and the last `infra` run summary. Likeliest first: a required setting that was never created (code deployed before infra), an out-of-range or malformed value (`AlertQuietHours` without a time zone, a bad CRON),
a Key Vault reference that does not resolve (secret missing, or the function identity lacks `Key Vault Secrets User`; restart after fixing),
`AlertWindowHours + AlertCooldownHours` above `RetentionDays * 24`. A mistyped key is silently ignored: the app starts and the setting has no effect.

## App runs but nothing arrives

- The digest always sends something (even "no posts"), so silence means the run never finished.
- Timer history: `requests | where name == "DigestFunction" | order by timestamp desc | take 20` (use the failing function's name; a manual run from `tools/run-now.cs` is logged as `RunNowFunction`). No rows means the schedule: CRON is UTC, check daylight saving.
- Failures hourly with "Collection failed": the Telegram session expired. Repeat the TgLogin step in `docs/user-guide/installation.md` (step 4).
- Scoring stuck: check the `to-score` and `to-score-poison` queue lengths; poisoned posts stay `New` until cleanup.
- OpenAI errors: the digest still arrives without a summary.

## Fix and record

Fix at the failing layer: a bad setting is fixed through `infra`, not by rolling back code. Roll back code (the `ship` skill, run manually as `/ship`) only if the new code caused it.
If a missing rule or step caused this, add it to the matching `CLAUDE.md` or `docs/user-guide/troubleshooting.md`.
