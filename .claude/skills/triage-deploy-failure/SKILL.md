---
name: triage-deploy-failure
description: Use when a build-test-deploy or infra workflow run failed, the function app does not start after a change, a scheduled digest or alert did not arrive, or messages pile up in a queue.
---

# Triage a deploy or runtime failure

## Overview

Find the failing layer first (pipeline, startup, schedule, queue, external service), then fix that layer only.
Read-only until the cause is known: never re-run a deploy, restart the app or change settings just to see what happens.

## Pipeline failed

- `gh run list --workflow build-test-deploy --limit 5`, then `gh run view <run-id> --log-failed`. Read the first failing step, not the last error.
- Build or tests: fix in code. Deploy step: check the `AZURE_*` variables, the pipeline identity's roles (ADR-0004) and `AZURE_FUNCTIONAPP_NAME`.
- Release step: Settings → Actions → Workflow permissions must allow the release job's `contents: write`.
- `infra` run failed: read the plan in the run summary; a state lock means another `infra` run is active.

## App does not start

Settings are validated on startup (`ValidateOnStart`), and a bad one stops the app with the parameter name in the log. In Application Insights run:

```
traces | where timestamp > ago(2h) and severityLevel >= 3 | order by timestamp desc | take 50
```

Most likely first: an out-of-range or malformed value (`AlertQuietHours` without a time zone, a bad CRON), a Key Vault reference that does not resolve
(secret missing, or the function identity lacks `Key Vault Secrets User`; restart after fixing), `AlertWindowHours + AlertCooldownHours` above `RetentionDays * 24`.
A mistyped key is silently ignored: the app starts and the setting simply has no effect.

## App runs but nothing arrives

- Digest or alert missing: schedules are UTC, so check the CRON against local time and daylight saving. Look for the timer's last runs and for "Collection failed" messages.
- The Telegram session may have expired: collection fails every hour. Repeat the TgLogin step in the README.
- Scoring stuck: check the `to-score` queue length and the `to-score-poison` queue; poisoned posts stay `New` until cleanup.
- OpenAI errors: the digest still arrives without a summary, with short scoring reasons.

## Fix and record

Fix at the failing layer, roll back code with the manual `deploy.yml` run (see the `ship` skill) only if the new release caused it.
If the cause was a missing rule or step, add it to the matching `CLAUDE.md` or the README "Known limitations".
