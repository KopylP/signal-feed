# Configuration

**Audience:** operators tuning their agent. Every parameter is an Azure Function App Setting.
New to the behavior behind these names? Read [How it works](how-it-works.md) first.

## Settings reference

| Parameter | Default | Description |
|---|---|---|
| `Sources` | six SEO channels (`infra/variables.tf`) | comma-separated usernames of channels and groups |
| `CollectorSchedule` | `0 0 * * * *` | collection CRON |
| `DigestSchedule` | `0 0 8 * * *` | digest CRON |
| `AlertSchedule` | `0 30 * * * *` | alert check CRON |
| `DigestSize` | 5 | posts in the digest (at most 8, otherwise the message exceeds the Telegram limit) |
| `InterestingThreshold` | 7 | score from which a post is marked "🔥 Цікаве" (interesting) |
| `ScoringBatchSize` | 10 | posts per queue message |
| `MaxPostChars` / `MinPostChars` | 1500 / 80 | truncation and minimum length |
| `MaxItemsPerGroup` | 5 | posts per group per run |
| `MaxBundleChars` | 3000 | bundle text size |
| `AlertSources` | | groups that can trigger an alert |
| `AlertThreshold` / `BurstMinPosts` | 9 / 8 | alert thresholds |
| `AlertWindowHours` / `AlertCooldownHours` | 3 / 3 | window of scored posts and pause between alerts for one group |
| `AlertMaxPerRun` | 3 | entries in one alert (at most 8) |
| `AlertQuietHours` / `AlertTimeZone` | empty / `Europe/Kyiv` | quiet hours |
| `OpenAIModel` | `gpt-5.4-nano` | scoring model |
| `OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash` | | **secrets**, Key Vault only |
| `TelegramPairingKey`, `TelegramWebhookSecret` | | **secrets**, Key Vault only: the key that binds your chat (16-64 characters of `A-Z a-z 0-9 _ -`) and the token Telegram sends with every webhook call (`A-Z a-z 0-9 _ -`) |
| `TelegramChatId` | empty | optional fixed chat id; when set it overrides the paired chat and turns pairing off |
| `TelegramApiId`, `TelegramPhone` | | account data for reading |
| `SessionBlobContainer` | `tg-session` | container with the Telegram session |
| `CleanupSchedule` | `0 0 3 * * *` | cleanup CRON (UTC) |
| `RetentionDays` | 1 | how many days back to keep posts: 1 means yesterday's and today's partitions |
| `FirstRunLookbackHours` | 24 | how many hours of history to read on the first run for a new source |

### Where the defaults live

The single source is `Shared/Settings/AgentOptions.cs`. The exception is `Sources`: its default is set
in `infra/variables.tf` (override it in `terraform.tfvars` or with the `TF_SOURCES` variable in GitHub). In Terraform, only the
required and infrastructure values get their own variables (sources, Telegram data, schedules, session container). Everything else is set through the `app_settings` variable
using the names from the table above, for example `app_settings = { DigestSize = "6", AlertQuietHours = "23:00-07:00" }`.
If a parameter is not set, the value from code applies. A typo in a key name is not an error, the parameter is simply ignored.

### Validation

Settings are validated on startup: required values (sources, OpenAI key, bot token, pairing key, webhook secret, Telegram data), ranges
(for example `DigestSize` and `AlertMaxPerRun` from 1 to 8, `ScoringBatchSize` 1–100, `MaxItemsPerGroup` 1–50, `RetentionDays` 1–365),
the `AlertQuietHours` format with a time zone, and that `AlertWindowHours + AlertCooldownHours` does not exceed `RetentionDays * 24`.
If something is wrong, the app does not start and the log names the parameter.

## How to change things

### Schedule

CRON has six fields (`second minute hour day month weekday`) and is set **in UTC**.
`0 0 8 * * *` is 08:00 UTC, which is 10:00 in Kyiv in winter (UTC+2) and 11:00 in summer (UTC+3), so you have to account for daylight saving time yourself.
Schedules are read through `%CollectorSchedule%` and the like, so you can change them without rebuilding the code. Just update the
App Setting (or the Terraform variable, for example `digest_schedule`, or the GitHub Variable `TF_DIGEST_SCHEDULE`, and run `terraform apply` or the `infra` workflow), and the app restarts with the new schedule. Precedence: a schedule key in `app_settings` (`TF_APP_SETTINGS`) wins over the dedicated variable (`TF_*_SCHEDULE`), which wins over the default.

```bash
az functionapp config appsettings set -g <rg> -n <function-app> \
  --settings DigestSchedule="0 0 6 * * *" CollectorSchedule="0 0 */2 * * *"
```

### Sources

`Sources` (and `AlertSources`) are comma-separated usernames; you do not have to strip `@` and `t.me/`:

```bash
az functionapp config appsettings set -g <rg> -n <function-app> \
  --settings Sources="channel_one,channel_two,some_group" AlertSources="some_group"
```

### Prompts

The files [`prompts/scoring.md`](../../prompts/scoring.md) (scoring) and [`prompts/digest.md`](../../prompts/digest.md) (digest summary) are copied into the build, so a change
needs a new deploy (a push to `main` is enough when [automatic deployment](../developer-guide/ci-cd.md#deploy-is-off-by-default) is on). **Before the first deploy**, fill in your own topics in `scoring.md` in place of `{тема 1}`
and examples in place of `{реальний пост...}`: otherwise the model will score against the literal placeholders.

## Next

Something not working as expected? See [troubleshooting](troubleshooting.md).
