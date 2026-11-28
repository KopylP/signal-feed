# Configuration

**Audience:** operators tuning their agent. Every parameter is an Azure Function App Setting. Many of them can also be changed from the Telegram bot ([see below](#from-telegram)); a value set there wins over the App Setting.
New to the behavior behind these names? Read [How it works](how-it-works.md) first.

## Settings reference

| Parameter | Default | Description |
|---|---|---|
| `Sources` | six SEO channels (`infra/variables.tf`) | comma-separated usernames of channels and groups |
| `CollectorSchedule` | `0 0 * * * *` | collection CRON |
| `DigestSchedule` | `0 0 8 * * *` | digest CRON |
| `DigestSize` | 7 | posts the model sees for the digest (1-12); a long digest is split into several Telegram messages |
| `InterestingThreshold` | 7 | score from which a post is marked "🔥 Цікаве" (interesting) |
| `ScoringBatchSize` | 10 | posts per queue message |
| `MaxPostChars` / `MinPostChars` | 1500 / 80 | truncation and minimum length |
| `MaxItemsPerGroup` | 5 | posts per group per run |
| `MaxBundleChars` | 3000 | bundle text size |
| `AlertSources` | | groups that can trigger an alert |
| `Watches` | empty | topics to follow, separated by `;` (for example `google core update;ai overviews`): a new scored post from any source that is about one by meaning, or has every word of it, is sent as its own alert. Normally set by asking the agent, not by hand |
| `WatchSimilarityThreshold` | 0.45 | how close in meaning (cosine similarity of the embeddings, 0 to 1) a post must be to a followed topic to match it; higher: fewer, surer matches; `1`: words only. Only an App Setting |
| `DuplicateWindowHours` | 3 | posts published further apart than this are never compared for duplicates (the same news usually comes within a few hours). Only an App Setting |
| `DuplicateSimilarityThreshold` | 0.9 | how close in meaning two single posts must be to count as one news; the later one is not scored and not in the digest. Lower: more posts dropped, with a risk of dropping different news; `1`: only exact copies. Only an App Setting |
| `AlertThreshold` / `BurstMinPosts` | 9 / 8 | alert thresholds |
| `AlertWindowHours` / `AlertCooldownHours` | 3 / 3 | window of scored posts and pause between alerts for one group |
| `AlertMaxPerRun` | 3 | entries in one alert (at most 8) |
| `AlertQuietHours` / `AlertTimeZone` | empty / `Europe/Kyiv` | quiet hours and the time zone they are read in |
| `TimeZone` | `Europe/Kyiv` | your time zone (IANA id): the times the agent shows and understands (the digest dates, the chat, search by date and time). Storage, schedules (CRON) and logs stay in UTC |
| `ManualRunsEnabled` | `false` | lets a message in the `run-now` queue start a job ([run a job now](#run-a-job-now)); keep it off except while testing |
| `OpenAIModel` | `gpt-5.4-nano` | scoring and digest model |
| `OpenAIChatModel` | `gpt-6.1-terra` | model that answers your questions about the digest (stronger than the scoring model; `gpt-6.1-sol` costs about twice as much) |
| `ChatMaxMessages` | 20 | messages kept in one conversation (a new digest starts a new one) |
| `ChatReasoningEffort` | `medium` | how much the chat model thinks before answering: `none`, `low`, `medium` or `high`; more is slower and costs more, `none` is the fallback if the chat fails after a model change |
| `ChatMaxOutputTokens` / `ChatMaxToolCalls` | 4000 / 5 | longest model output including the hidden reasoning, and how many times per question the model may stop to use tools (one round can call several tools); a question costs up to `ChatMaxToolCalls + 1` model calls |
| `ChatSearchMaxHits` | 8 | posts returned by one search of stored posts |
| `StatsRetentionDays` | 90 | how many days the daily summaries (counts per source and topic, the best posts, no post text) are kept for `/stats` and for the agent's trends: 7-365 |
| `FeedbackEnabled` | `true` | rating posts by telling the agent («+», «лайк», «мінус» as a reply to a news item), and the recent votes as a hint in every scoring request; `false`: the agent does not save votes and scoring gets no hint (votes already given stay in `settings/feedback.json`) |
| `ChatStreamEnabled` | `true` | shows the chat answer as it is written (a Telegram draft with «💭 Думаю», then the real message); `false` sends it at once |
| `OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash` | | **secrets**, Key Vault only |
| `TelegramPairingKey`, `TelegramWebhookSecret` | | **secrets**, Key Vault only: the key that binds your chat (16-64 characters of `A-Z a-z 0-9 _ -`) and the token Telegram sends with every webhook call (`A-Z a-z 0-9 _ -`) |
| `TelegramChatId` | empty | optional fixed chat id; when set it overrides the paired chats, turns pairing off and means exactly one chat |
| `TelegramMaxChats` | 5 | how many chats can be paired at once (1-20); each gets the digest, alerts and failure messages |
| `TelegramApiId` | | app id of the reader account (from my.telegram.org) |
| `SessionBlobContainer` | `tg-session` | container with the Telegram session |
| `SessionKeyId` | set by Terraform | versionless id of the Key Vault key that encrypts the session and the sign-in in progress ([ADR-0017](../adr/0017-encrypt-the-telegram-session-with-a-key-vault-key.md)); required in Azure, empty only locally against Azurite |
| `DailySummarySchedule` | `0 0 3 * * *` | CRON (UTC) of the daily summary of the finished days; the cleanup of old data starts right after it succeeds (was `CleanupSchedule`) |
| `RetentionDays` | 2 | how many days back to keep posts: 2 means the day before yesterday's, yesterday's and today's partitions; it is also how far back the chat can search by date |
| `FirstRunLookbackHours` | 24 | how many hours of history to read on the first run for a new source |

### Where the defaults live

The single source is `Shared/Settings/AgentOptions.cs`. The exception is `Sources`: its default is set
in `infra/variables.tf` (override it in `terraform.tfvars` or with the `TF_SOURCES` variable in GitHub). In Terraform, only the
required and infrastructure values get their own variables (sources, Telegram data, schedules, session container). Everything else is set through the `app_settings` variable
using the names from the table above, for example `app_settings = { DigestSize = "6", AlertQuietHours = "23:00-07:00" }`.
If a parameter is not set, the value from code applies. A typo in a key name is not an error, the parameter is simply ignored.

### Validation

Settings are validated on startup: required values (sources, OpenAI key, bot token, pairing key, webhook secret, Telegram data), ranges
(for example `DigestSize` from 1 to 12, `AlertMaxPerRun` from 1 to 8, `ScoringBatchSize` 1–100, `MaxItemsPerGroup` 1–50, `RetentionDays` 1–365),
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

### From Telegram

Send `/settings` to the bot in the private chat. One message with buttons opens and is edited as you tap: 📡 Sources, 📰 Digest, 🚨 Alerts, 📥 Collecting, 💬 Chat, 🧠 Models.

- **Numbers** change with the `−` and `+` buttons, inside the allowed range (the same ranges as in the table above).
- **Fixed choices** (the models, the chat's reasoning effort) are buttons; you never type a model name.
- **Typed values** (quiet hours, time zone, a new channel): the bot asks you to send the value. You have 10 minutes; `/cancel` or any other command abandons the question. For quiet hours send `-` to switch them off.
- **✎** marks a value set from the bot, **↺** returns it to the App Setting.
- **Sources**: add a channel by `@name`, `t.me/name` or a link. It shows ⏳ until the first collection reads it (✅); if the read fails the bot tells you and offers «🗑 Видалити». 🚨 turns a source into an alert source. The last source cannot be removed.
- A change reaches the jobs on their next run (a running job can use the old value for about 30 seconds).
- A combination that breaks a rule is refused with the names of the fields, for example `AlertWindowHours + AlertCooldownHours` above `RetentionDays * 24`.

Precedence: the bot, then the App Setting, then the default in code. Once you change the source list in the bot, a later change of the `Sources` App Setting is ignored until you reset the sources group.
Not available in the bot: schedules, the fixed parts of the prompts, secrets, `RetentionDays`, `ManualRunsEnabled`, `TelegramChatId`, `TelegramMaxChats`, the Telegram account data and storage names.

### By asking the agent

The same settings and sources can be changed by writing to the agent in the private chat, after a digest: «прибери один ресурс», «зроби алерти рідшими», «постав модель чату gpt-6.1-terra».
The agent asks when the request is not clear («який саме?», with your sources or the matching settings as tiles to tap in place of the keyboard) and may collect several changes into one plan.
It never changes settings, sources or prompts itself: the bot sends a separate message «⚙️ Агент пропонує змінити налаштування» with the exact changes (the old and the new value) and the buttons «✅ Застосувати» / «✖ Скасувати».
The text of that message is made by the bot, not by the model, so check it rather than the agent's wording. The plan waits 30 minutes; the changes are applied together or not at all, with the same rules and limits as in `/settings`.

Following a topic («стеж за темою google core update») is not a setting change in this sense: the agent does it at once, like rating a post or clearing the conversation ([ADR-0021](../adr/0021-let-the-chat-agent-do-non-invasive-actions-at-once.md)).

The buttons need the webhook to receive `callback_query` updates ([installation](installation.md#step-6-connect-your-chat)).

### Run a job now

To test a job without waiting for its schedule, put its name into the `run-now` queue with [`tools/run-now.cs`](../../tools/run-now.cs).
The jobs are `collect`, `digest`, `alert` and `daily-summary` (the last one also starts the cleanup, as the timer does). `digest` and `alert` send a real Telegram message and mark posts as sent, so the next scheduled run sees fewer posts.

```bash
az functionapp config appsettings set -g <rg> -n <function-app> --settings ManualRunsEnabled=true   # restarts the app
az login   # your account needs "Storage Queue Data Contributor" on the storage account (granted by Terraform to deployer_object_id)
export AZURE_TOKEN_CREDENTIALS=AzureCliCredential   # use only your az login; without it the credential lookup can hang on some machines
dotnet run tools/run-now.cs -- digest --storage <storage-account-name>
az functionapp config appsettings set -g <rg> -n <function-app> --settings ManualRunsEnabled=false
```

The command only queues the job and does not wait for it: read the result in the function logs (a manual run is logged under `RunNowFunction`, not under the timer function). `collect` moves the read cursors and reports failed sources to Telegram, and running `digest` right after the scheduled digest sends the "no posts" message. Two runs at the same time (or one together with the timer) can send a message twice. Two collections never run at once: they share the Telegram session, so a `collect` that starts while another holds it is skipped with a warning in the log. While `ManualRunsEnabled` is `false`, a message is dropped with a warning.
A job that fails is logged and not retried, like a scheduled run. Access to the queue is controlled by Azure roles, there is no key.

### Prompts from the chat

Most of the prompts can be tuned by asking the agent, without a deploy ([ADR-0025](../adr/0025-let-the-chat-agent-rewrite-whole-prompt-sections.md)). Only the technical part is fixed: the output fields and their limits, the format of the input, the tool names, and the rules that treat post text as data or forbid the agent to change anything on its own.
The editable sections are:

- *scoring*: the role and the goal, my work, my interests, the scale, how to judge a post;
- *digest*: the role and the length, about me, the "can be skipped" note, how to judge and select;
- *chat*: the role, the style of the answers, how to look things up, offering buttons, when to act at once, how to talk about settings, how to keep notes.

- Ask what is in a prompt: «що в мене в промпті оцінювання?». The agent reads it (`get_prompt`) and tells you, naming the sections you can change.
- Ask for a small change: «додай у мої інтереси Bing та Yandex». The agent proposes **one fragment replaced by another**.
- Ask for a big one: «скороти промпт чату вдвічі», «перепиши секцію про стиль коротше». The agent proposes a **whole rewrite** of the section (`propose_prompt_rewrite`).
- The bot sends «⚙️ Агент пропонує змінити налаштування» and the buttons «✅ Застосувати» / «✖ Скасувати». For a small change the message has the exact old text (−) and new text (+). For a rewrite the changed lines come in the messages just before it (− removed, + added, … for lines that stay), with the length before and after. Nothing changes until you tap.
- A plan holds at most 6 text steps (edits and rewrites together), a fragment is up to 1500 characters, a section up to 6000. A section cannot get headings and cannot be empty; to go back, ask «поверни стандартний промпт секції …» (a reset).
- The change applies within about 10 seconds. «Скинути все» in `/settings` returns the prompts to the shipped text.
- A section you rewrote keeps your text when a later release improves the shipped one; a reset of that section brings the new shipped text.

### Reader memory

The agent keeps short notes about you (your work, projects, goals, interests, how you like answers) and sees them with every question, so answers fit you ([ADR-0022](../adr/0022-keep-a-reader-memory-the-chat-agent-manages-itself.md)). It replaces the earlier «про мене» profile: an existing profile becomes the first note, «Про мене», on your first question after the update.

- **The agent decides what to keep.** When you tell it a lasting fact about yourself («я працюю над каталогом майданчиків»), it saves or updates a note at once, without a confirmation; it removes or corrects a note when you ask («забудь про …», «я більше не займаюсь …») or when a note is no longer true. It saves only what you said yourself, never text from posts.
- **You always see a change.** A line under the answer says what changed: «🧠 Запам'ятав: …», «🧠 Оновив нотатку: …» or «🧠 Забув: …». Ask «що ти про мене пам'ятаєш?» to see it all.
- **Short and structured.** At most 10 notes per chat, one topic each, at most 50 lines (best about 20). Each is a Markdown file named by the day it was last changed and its keywords, `settings/reader-memory/<chat id>/2026-11-27-work-seo.md`, with a heading, `Keywords:`, `Updated:` and one fact per line. When the memory is full, the agent merges or removes a note before adding one.
- Every paired chat has its own memory and never sees another's. «Скинути все» and `/clear` do not touch it; the chat uses it, scoring and the digest do not. To remove everything, ask the agent to forget each note, or delete the blobs in the portal.

### Prompt files

The files [`prompts/scoring.md`](../../prompts/scoring.md) (scoring), [`prompts/digest.md`](../../prompts/digest.md) (digest summary) and [`prompts/chat.md`](../../prompts/chat.md) are copied into the build, so a change
needs a new deploy (a push to `main` is enough when [automatic deployment](../developer-guide/ci-cd.md#deploy-is-off-by-default) is on). **Before the first deploy**, check the topics under `My interests` in `scoring.md`: the model scores every post against them.
The prompts are English and ask the model to answer in Ukrainian.

## Next

Something not working as expected? See [troubleshooting](troubleshooting.md).

## Rotating the session key

The Telegram session and the sign-in in progress are encrypted with the Key Vault key `tg-session` ([ADR-0017](../adr/0017-encrypt-the-telegram-session-with-a-key-vault-key.md)).
Nothing needs to run for its rotation: Key Vault creates a new version every 90 days (the rotation policy in `infra/keyvault.tf`).
Old versions stay enabled so that older blobs can still be read, and every rewrite uses the newest version: the session is rewritten on every collection,
so within an hour after a rotation everything is encrypted with it. A version expires a year after it is created; an expired version no longer encrypts new data.

To rotate now (for example after a handover, or if you think a storage key or a backup leaked):

```bash
KV=$(terraform -chdir=infra output -raw key_vault_name)
az keyvault key rotate --vault-name $KV --name tg-session
```

Then wait for the next collection, check in the log that it succeeded, and disable the older versions so that old copies of the session cannot be opened:

```bash
CURRENT=$(az keyvault key show --vault-name $KV --name tg-session --query key.kid -o tsv)
az keyvault key list-versions --vault-name $KV --name tg-session --query "[?kid!='$CURRENT'].kid" -o tsv |
  while read -r kid; do az keyvault key set-attributes --id "$kid" --enabled false >/dev/null; done
```

If the session cannot be read afterwards (a collection fails with a Key Vault error), sign in again with `/login`: a new session is written with the current version.
Your account needs "Key Vault Crypto Officer" on the vault for these commands; `terraform apply` grants it to whoever runs Terraform.
