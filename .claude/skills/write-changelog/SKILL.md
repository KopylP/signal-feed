---
name: write-changelog
description: Use when a change that alters behavior, a contract or a setting is finished, before it is committed or opened as a pull request, or when asked to write, fix or review a changelog entry or release notes.
---

# Write a changelog entry

## Overview

`CHANGELOG.md` tells the person who runs the agent what changed and what they must do about it. It is not a commit log.
Every pull request that changes behavior, a contract or a setting adds **one section** to it, in the same commit as the change.
Write for the operator: what is different, and what to do. Never describe how the code was rearranged.

## When an entry is needed

| Change | Entry? |
|---|---|
| Changes what the agent does, sends, stores or reads | Yes |
| Changes a setting, secret, variable, schedule or manual step | Yes |
| Bug fix the operator could notice | Yes |
| Dependency update | Yes, one line (`Dependencies`) |
| Refactoring, tests, CI, formatting, docs, comments with no visible effect | **No** |

If unsure whether an operator would notice, write the entry. If a PR has no entry, say so in the PR description ("no changelog: internal only").

## Choose the category

Ask in this order. One change can need two entries (for example a new required setting is both `Added` and `Setup required`).

1. **Must the operator do something before or after the deploy?** (set a secret, a variable, a setting, run a command) → `Setup required`.
2. **Did something others depend on change its shape, name or meaning?** → `Contract changes`.
3. **Is it a capability that did not exist?** → `Added`.
4. **Is it the same capability with a different rule, algorithm or threshold?** → `Logic`.
5. Otherwise use a short category: `Improved`, `Fixed`, `Security`, `Removed / Deprecated`, `Dependencies`.

A new trigger or condition for something that already exists (an extra reason to send an alert, a new threshold) is `Logic`; a new function, message type or setting-driven feature is `Added`.
An optional setting with a default is only `Added` (or `Logic`, if the default changes behavior). It is `Setup required` only when the operator must act.

## Structure of the file

Newest section first, under the file header. Inside a section, categories appear in this fixed order and empty ones are left out:

`Setup required` → `Contract changes` → `Added` → `Logic` → `Improved` → `Fixed` → `Security` → `Removed / Deprecated` → `Dependencies`

The first four are **detailed**: they use the templates below. The rest are **short**: one line each.

### Section heading

The version is unknown until the pipeline runs, so a pull request names its section with a **word** and today's UTC date (ADR-0007):

```
## [falcon] - 2026-11-24
```

Get a word that is not used yet with `bash .claude/skills/write-changelog/scripts/new-word.sh`, once per pull request. Never invent a version number and never write `v...` yourself.
After the deploy the pipeline puts the build version in front of the word and keeps the word:

```
## [v2026.11.24.131-falcon] - 2026-11-24
```

The pipeline also uses the word as the release name (tag `v2026.11.24.131-falcon`) and the section text as the release notes.
Add the section at the top of the file. If two pull requests collide there, keep both sections.
A change that merges without a pipeline run (for example together with `infra/**`) keeps its word until the next deploy, which names it too.

## Detailed categories

Fill every field. If a field does not apply, write `none` rather than dropping it, so the reader knows it was considered.

### Setup required

What the operator must do. Put it first in the section: it blocks the deploy.

```
### Setup required
- **`OpenAIModel`**: required | optional (default `gpt-5.4-nano`)
  - Where: Key Vault | App Setting (`app_settings`) | Terraform variable | GitHub Variable (`TF_*`) | `local.settings.json`
  - Value: what to put there, and how to obtain it
  - Apply: the exact command or workflow, and the order (infra and secrets before the code merges)
```

Rules: list every name exactly as it is spelled in Azure, Terraform and GitHub. State plainly if the app fails to start without it. Never write a secret value, only where it goes.

### Contract changes

A contract is anything outside the code that something else depends on:

- Telegram messages: layout, wording, markup (quote the Ukrainian text as it is sent)
- App Settings: names, meaning, default, allowed range
- Storage: `Posts` and `ChannelState` columns, `to-score` message body, container and queue names
- Model I/O: the response schema, prompt placeholders in `prompts/`
- Terraform: variable, output and resource names (a rename can recreate a resource)
- Function names and triggers

```
### Contract changes
- **<surface>: <what changed>** (breaking | compatible)
  - Before: the old shape, name or value
  - After: the new one
  - Affects: who or what breaks, or `nothing`
  - Migration: exact steps, or `none`
```

Rules: mark every entry `breaking` or `compatible`. A breaking change needs a migration, even if the migration is "delete the old value". Show before and after literally, not "improved".

### Added

A capability that did not exist.

```
### Added
- **<feature name>**: what it does for the reader, in one sentence.
  - Enable: how it is turned on, or `on by default`
  - Settings: the settings it adds, with defaults, or `none`
  - Limits: what it does not do, or `none`
```

Rules: describe the effect, not the class names. Link the section of the user guide that explains it.

### Logic

A different rule, algorithm or threshold in the key business logic: collecting, scoring, bundling, alert selection, digest selection, retention.

```
### Logic
- **<area>: <the rule that changed>**
  - Before: the old rule, with numbers
  - After: the new rule, with numbers
  - Effect: what the reader will see differently (digest, alerts, cost, volume)
```

Rules: write the rule in words and numbers a non-developer can check. Say what the reader will notice. A tuned default belongs here, not under `Improved`.

## Short categories

One line, the symptom or effect first, no class names:

- `Improved`: faster, cheaper, clearer, with no change of rule or contract. Example: "Digest is built 3x faster for large groups."
- `Fixed`: what was wrong and when. Example: "Fixed a duplicate alert when a group stayed busy across two runs."
- `Security`: what was exposed or hardened, without a recipe for abusing it.
- `Removed / Deprecated`: what goes away and what replaces it. A removed contract is also a `Contract changes` entry.
- `Dependencies`: package and version, for example "Telegram.Bot 22.7 → 22.8". Group routine updates in one line.

## Writing rules

- English, past tense for `Fixed`, present tense for the rest. Project terms follow the README (digest, alert, source, bundle).
- One idea per entry. No commit hashes. A pull request number in parentheses at the end is welcome: `(#12)`.
- Say what changed for the operator, then why if it is not obvious. Never "refactored", "cleaned up" or "various fixes".
- Do not copy the pull request title. A title is a release note headline; the entry carries the detail.

## Release step

Automatic, in `deploy.yml` (ADR-0007): after a deploy the release job creates the release `vYYYY.MM.DD.N-word` with the section as notes, renames the heading
and commits that to `main`. You do nothing after the merge. Run `python3 .github/scripts/changelog.py validate` before a pull request (`check-before-pr` does it): it rejects
a wrong heading, an unknown or misordered category and a duplicate word.

## Common mistakes

| Mistake | Fix |
|---|---|
| A version number in a pull request | Use a word from `new-word.sh`; the pipeline owns the version |
| "Updated the scoring" | Say which rule, before and after, with numbers (`Logic`) |
| A new required setting listed only under `Added` | Also `Setup required`, with the exact name, place and order |
| A renamed setting under `Improved` | It is a breaking `Contract changes` entry with a migration |
| An entry for a refactor | Remove it |
| Empty `Setup required` heading | Leave empty categories out |
| A secret value in an example | Show where it goes, never the value |

## Example

Illustrative only: these are not real changes, and the numbers and names are made up.

```
## [falcon] - 2026-11-24

### Setup required
- **`AlertQuietHours`**: optional (default empty)
  - Where: App Setting (`app_settings`)
  - Value: `HH:mm-HH:mm`, for example `23:00-07:00`, in `AlertTimeZone`
  - Apply: `terraform apply` or the `infra` workflow; no need to wait for the code deploy

### Added
- **Quiet hours for alerts**: no alerts are sent during the chosen hours.
  - Enable: set `AlertQuietHours`
  - Settings: `AlertQuietHours`, `AlertTimeZone` (default `Europe/Kyiv`)
  - Limits: the digest is not affected

### Logic
- **Alerts: posts held back by quiet hours**
  - Before: a post that qualified for an alert was dropped if the alert was not sent
  - After: it goes into the next digest
  - Effect: nothing important is lost overnight

### Fixed
- Fixed a repeated alert when the "sent" mark could not be saved. (#14)
```
