# Configuration

**Audience:** operators tuning their agent. Every parameter is an Azure Function App Setting. Many of them can also be changed from the Telegram bot ([see below](#from-telegram)); a value set there wins over the App Setting.
New to the behavior behind these names? Read [How it works](how-it-works.md) first.

## Settings reference

| Parameter | Default | Description |
|---|---|---|
| `Sources` | six SEO channels (`infra/variables.tf`) | comma-separated usernames of channels and groups |
| `CollectorSchedule` | `0 0 * * * *` | collection CRON |
| `DigestSchedule` | `0 0 8 * * *` | digest CRON |
| `AlertSchedule` | `0 30 * * * *` | alert check CRON |
| `DigestSize` | 7 | posts the model sees for the digest (1-12); a long digest is split into several Telegram messages |
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
| `ManualRunsEnabled` | `false` | lets a message in the `run-now` queue start a job ([run a job now](#run-a-job-now)); keep it off except while testing |
| `OpenAIModel` | `gpt-5.4-nano` | scoring and digest model |
| `OpenAIChatModel` | `gpt-6.1-terra` | model that answers your questions about the digest (stronger than the scoring model; `gpt-6.1-sol` costs about twice as much) |
| `ChatMaxMessages` | 20 | messages kept in one conversation (a new digest starts a new one) |
| `ChatReasoningEffort` | `medium` | how much the chat model thinks before answering: `none`, `low`, `medium` or `high`; more is slower and costs more, `none` is the fallback if the chat fails after a model change |
| `ChatMaxOutputTokens` / `ChatMaxToolCalls` | 4000 / 5 | longest model output including the hidden reasoning, and how many times per question the model may stop to use tools (one round can call several tools); a question costs up to `ChatMaxToolCalls + 1` model calls |
| `ChatSearchMaxHits` | 8 | posts returned by one search of stored posts |
| `OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash` | | **secrets**, Key Vault only |
| `TelegramPairingKey`, `TelegramWebhookSecret` | | **secrets**, Key Vault only: the key that binds your chat (16-64 characters of `A-Z a-z 0-9 _ -`) and the token Telegram sends with every webhook call (`A-Z a-z 0-9 _ -`) |
| `TelegramChatId` | empty | optional fixed chat id; when set it overrides the paired chats, turns pairing off and means exactly one chat |
| `TelegramMaxChats` | 5 | how many chats can be paired at once (1-20); each gets the digest, alerts and failure messages |
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
The agent asks when the request is not clear («який саме?», with the list of your sources or the matching settings) and may collect several changes into one plan.
It never changes anything itself: the bot sends a separate message «⚙️ Агент пропонує змінити налаштування» with the exact changes (the old and the new value) and the buttons «✅ Застосувати» / «✖ Скасувати».
The text of that message is made by the bot, not by the model, so check it rather than the agent's wording. The plan waits 30 minutes; the changes are applied together or not at all, with the same rules and limits as in `/settings`.

The buttons need the webhook to receive `callback_query` updates ([installation](installation.md#step-6-connect-your-chat)).

### Run a job now

To test a job without waiting for its schedule, put its name into the `run-now` queue with [`tools/run-now.cs`](../../tools/run-now.cs).
The jobs are `collect`, `digest`, `alert` and `cleanup`. `digest` and `alert` send a real Telegram message and mark posts as sent, so the next scheduled run sees fewer posts.

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

Parts of the prompts can be tuned by asking the agent, without a deploy. The editable sections are: *scoring*: my work, my interests, the scale; *digest*: about me; *chat*: the style of the answers.
Everything else (the output fields and limits, the rules that treat post text as data, the tool names) is fixed in the files and cannot be changed from the bot.

- Ask what is in a prompt: «що в мене в промпті оцінювання?». The agent reads it (`get_prompt`) and tells you, naming the sections you can change.
- Ask for a change: «додай у мої інтереси Bing та Yandex». The agent finds the section and proposes **one fragment replaced by another**. The bot then sends «⚙️ Агент пропонує змінити налаштування» with the exact old text (−) and the new text (+) and the buttons «✅ Застосувати» / «✖ Скасувати». Nothing changes until you tap.
- A plan holds at most 3 text edits, each fragment up to 500 characters, a section up to 2000. A section cannot get headings and cannot be empty; to go back, ask «поверни стандартний промпт секції …» (a reset).
- The change applies within about 10 seconds. «Скинути все» in `/settings` returns the prompts to the shipped text.

### About me

A short text about you (your work, goals, what you do not care about) that the chat always sees as context, so answers fit you. It is stored per chat in its own blob (`settings/profile/<chat id>.txt`), not in the settings: every paired chat has its own and never sees another's, and «Скинути все» does not clear it.
Tell the agent: «запам'ятай, що я працюю над каталогом майданчиків», or ask what it knows: «що ти про мене знаєш?». Changes work like prompt edits: the agent proposes an exact before and after, and you confirm with the button. The profile is used by the chat only, not by scoring or the digest.

### Prompt files

The files [`prompts/scoring.md`](../../prompts/scoring.md) (scoring), [`prompts/digest.md`](../../prompts/digest.md) (digest summary) and [`prompts/chat.md`](../../prompts/chat.md) are copied into the build, so a change
needs a new deploy (a push to `main` is enough when [automatic deployment](../developer-guide/ci-cd.md#deploy-is-off-by-default) is on). **Before the first deploy**, check the topics under `My interests` in `scoring.md`: the model scores every post against them.
The prompts are English and ask the model to answer in Ukrainian.

## Next

Something not working as expected? See [troubleshooting](troubleshooting.md).
