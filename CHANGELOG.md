# Changelog

What changed in Signal Feed and what the operator must do about it. Newest first.
This is not a commit log: only changes that alter behavior, a contract or a setting are listed.

Each pull request adds one section named by a random word, `## [falcon] - 2026-11-24`. After the deploy the pipeline puts the build version in front of the word:
`## [v2026.11.24.131-falcon] - 2026-11-24`, and uses the section as the release notes (ADR-0007).
The structure, categories and templates are in the `write-changelog` skill (`.claude/skills/write-changelog/SKILL.md`).
Categories, in order: Setup required, Contract changes, Added, Logic, Improved, Fixed, Security, Removed / Deprecated, Dependencies.

## [clover] - 2026-11-25

### Added
- **The chat works before the first digest**: the agent answers from the stored posts and its own knowledge, and says which; the first digest replaces that conversation with its own
- **Clear the context**: `/clear` forgets the earlier questions and answers and keeps the digest; asking the agent to forget the conversation makes it prepare the action, and a button confirms it

### Logic
- The bot no longer answers «Ще немає дайджесту...»: a question before the first digest starts an empty conversation (a blob `chat-state/<chat id>.json` with no report id)

## [v2026.11.25.61-yarrow] - 2026-11-25

### Setup required
None. The new blobs `settings/profile/<chat id>.txt` are created on the first save, in the container the app already uses.

### Contract changes
- **Storage: `settings/overrides.json` gets `promptSections`** (compatible)
  - Before: values, sources, alert sources, pending question, proposals
  - After: also the owner's text for editable prompt sections (section id to text); a missing field means the shipped prompts
  - Affects: nothing to do; "Скинути все" in `/settings` now also returns the prompts to the shipped text
  - Migration: none
- **Storage: new blobs `settings/profile/<chat id>.txt`** (compatible)
  - One per chat: what the reader of that chat wrote about themselves; not cleared by "Скинути все"

### Added
- **Edit prompt sections from the chat**: the agent shows a prompt (`get_prompt`) and proposes one fragment replaced by another in an editable section; the bot shows the exact before and after, and a tap applies it ([configuration](docs/user-guide/configuration.md#prompts-from-the-chat), ADR-0015)
  - Editable: scoring (my work, my interests, the scale), digest (about me), chat (style of the answers); the output fields, limits and data rules stay fixed
- **Reader profile**: a short "about me" the chat always sees; changed through the same confirmed edit

### Logic
- Scoring, the digest and the chat read their prompt through a provider that applies the owner's edits (cached for 10 seconds), so a change needs no deploy
- The chat prompt keeps the answer style in its own section and has a rule for `<about_me>`

## [v2026.11.25.58-heather] - 2026-11-25

### Fixed
- **The chat understands a reply and a quote**
  - Before: a message that replied to another one, or quoted a part of it, reached the model without the quoted text, so "expand on this" had nothing to point at
  - After: the quoted part (or, with no quote, the message replied to) goes to the model with the question, as data
  - Affects: the chat about the digest

## [v2026.11.25.56-sorrel] - 2026-11-25

### Setup required
- **`TelegramMaxChats`**: optional (default `5`)
  - Where: App Setting (`app_settings`)
  - Value: how many chats can be paired at once, 1 to 20
  - Apply: nothing is needed; to change it, set it in `app_settings` and run the `infra` workflow. Nothing in Azure has to exist before the code deploys.

### Contract changes
- **Storage: table `ChatBinding` holds one row per chat** (compatible)
  - Before: one row `chat`/`digest` with the paired chat
  - After: one row `chat`/`<chat id>` per paired chat; the old `digest` row still counts as a chat
  - Affects: the command that unbinds a chat (the row key is now the chat id, not `digest`), see [installation](docs/user-guide/installation.md#step-6-connect-your-chat)
  - Migration: none
- **Storage: one conversation blob per chat** (compatible)
  - Before: `chat-state/current.json`
  - After: `chat-state/<chat id>.json`
  - Affects: the conversation that was open at the deploy is not carried over; the next digest starts a new one in every chat, and the cleanup removes the old file
  - Migration: none
- **Storage: `chat-messages` queue message carries `chatId`** (breaking for queued messages)
  - Before: `updateId`, `text`, `reportId`
  - After: also `chatId`
  - Affects: a question that waits in the queue at the deploy is dropped (it lives one hour at most)
  - Migration: none; ask again
- **Storage: `settings/overrides.json` gets an optional `ChatId` in the pending question and in each proposal** (compatible)
  - Before: they belonged to "the" chat
  - After: they belong to the chat that was asked; a value without it can be used by any chat
  - Affects: nothing; an old blob reads as before
  - Migration: none

### Added
- **Several chats**: every chat that sends `/start <TelegramPairingKey>` is added and gets the same digest, alerts and failure messages; each has its own conversation and `/settings`, while settings and sources are shared. Use it for a second reader or a test account.
  - Enable: on by default; send the pairing link to each chat
  - Settings: `TelegramMaxChats` (5)
  - Limits: a chat over the limit is told so and not added; a plan prepared by the agent can be confirmed only in the chat that got it; `TelegramChatId`, when set, still means exactly one chat. See [how it works](docs/user-guide/how-it-works.md#asking-about-the-digest) and [ADR-0014](docs/adr/0014-serve-several-chats-with-shared-settings.md).
- **Handover guide**: [moving the agent to another person](docs/user-guide/handover.md), with clearing the history and getting the first digest.
  - Enable: documentation only
  - Settings: none
  - Limits: none

### Logic
- **Pairing: no longer "the first chat wins"**
  - Before: the first chat that paired was the only one; others were ignored even with the right key
  - After: every chat with the right key is added until `TelegramMaxChats`
  - Effect: whoever has the pairing key can join, so change `TelegramPairingKey` (and restart the app) when the key must stop working

## [v2026.11.25.54-thistle] - 2026-11-25

### Contract changes
- **Storage: `settings/overrides.json` gets an optional `Proposals` list** (compatible)
  - Before: values, sources, alert sources, the pending question, handled update ids
  - After: also the plans that the agent prepared and that wait for a button (the last five, each valid 30 minutes)
  - Affects: nothing; a blob without the field reads as before
  - Migration: none
- **Telegram: new message «⚙️ Агент пропонує змінити налаштування»** (compatible)
  - Before: the bot sent only digests, alerts, failures, answers and the `/settings` screens
  - After: it also sends the plan the agent prepared, with «✅ Застосувати» / «✖ Скасувати» (callback data `pa:<id>` and `pn:<id>`)
  - Affects: buttons of an old message keep working only for 30 minutes
  - Migration: none

### Added
- **Change settings by asking the agent**: write «прибери один ресурс» or «зроби алерти рідшими» in the chat; the agent asks what is unclear (with the list), prepares a plan and the bot shows it with a confirm button. Nothing changes without the tap.
  - Enable: on by default, after the first digest (the chat needs one)
  - Settings: none
  - Limits: the same parameters and ranges as `/settings`; secrets, schedules and prompts cannot be changed. See [configuration](docs/user-guide/configuration.md#by-asking-the-agent).

## [v2026.11.25.51-linden] - 2026-11-25

### Setup required
- **Terraform apply before the deploy**: required
  - Where: the `infra` workflow (plan, then apply)
  - Value: creates the blob container `settings`; no new variables or secrets
  - Apply: run `infra` first, then merge. Without the container the bot cannot save settings, and the jobs keep using the App Settings
- **Telegram webhook update types**: required for the buttons
  - Where: Telegram Bot API (`setWebhook`)
  - Value: `allowed_updates=["message","callback_query"]`; the call is in [installation, step 6](docs/user-guide/installation.md#step-6-connect-your-chat)
  - Apply: after the deploy, run the same `setWebhook` call again. Until then `/settings` opens but its buttons do nothing

### Contract changes
- **Telegram webhook: handles button presses** (compatible)
  - Before: only `message` updates were handled
  - After: `callback_query` updates (button presses) from the bound private chat are handled too; `/start` still pairs
  - Affects: the webhook registration (see Setup required); presses from other chats and groups are ignored
  - Migration: run `setWebhook` again with both update types
- **Storage: new blob `settings/overrides.json`** (compatible)
  - Before: none
  - After: what you set from the bot; replaced on every change
  - Affects: nothing existing
  - Migration: none
- **Settings: a value set in the bot wins over the App Setting** (compatible)
  - Before: App Setting, then the default in code
  - After: the bot, then the App Setting, then the default; applies to the settings the bot can change ([list](docs/user-guide/configuration.md#from-telegram))
  - Affects: once the sources are changed in the bot, a later change of the `Sources` App Setting is ignored until the group is reset in the menu
  - Migration: none

### Added
- **Settings from the Telegram bot**: `/settings` opens a menu with buttons to change the digest size and thresholds, alerts, collecting limits, chat limits and the models, without a restart.
  - Enable: on by default
  - Settings: none new
  - Limits: schedules, prompts, secrets and `RetentionDays` are not in the menu; see [configuration](docs/user-guide/configuration.md#from-telegram)
- **Sources from the Telegram bot**: add, remove and mark alert sources in the menu. A new source shows ⏳ until the first collection reads it.
  - Enable: on by default
  - Settings: none new
  - Limits: the last source cannot be removed; the bot does not check that the reading account can see a channel until the collection

### Logic
- **Settings are read at the start of each run**
  - Before: read when the app started, so a change needed a restart
  - After: read at the start of every run (cached for 30 seconds); the model of scoring, the digest and the chat is chosen per request
  - Effect: a change from the bot applies to the next run; changing an App Setting still restarts the app

### Improved
- The «Збір не вдався» message offers «🗑 Видалити» for a source added from the bot that failed before it was ever read.
- Cursors of sources that are no longer listed are deleted, so adding one back later starts as a new source.

## [v2026.11.25.47-aspen] - 2026-11-25

### Setup required
- **Terraform apply before the deploy**: required
  - Where: the `infra` workflow (plan, then apply)
  - Value: creates the queue `chat-messages` and the blob container `chat-state`; no new variables or secrets
  - Apply: run `infra` first, then merge. Without the container the chat state cannot be saved: the digest still arrives, but the bot cannot answer questions about it

### Contract changes
- **Telegram webhook: handles every message, not only `/start`** (compatible)
  - Before: any message except `/start <key>` was ignored
  - After: a text message in the bound private chat is queued and answered by the chat; `/start` still pairs
  - Affects: `setWebhook` already asks for `message` updates, so nothing to change; messages from other chats, groups and non-text messages are still ignored
  - Migration: none
- **Storage: new queue `chat-messages` and blob `chat-state/current.json`** (compatible)
  - Before: none
  - After: questions wait in `chat-messages` for up to an hour; the blob holds the conversation about the latest digest and is replaced by every digest
  - Affects: nothing existing
  - Migration: none

### Added
- **Chat about the digest**: reply to the bot after a digest to ask more; an AI assistant answers with the digest in mind, can open full posts and search the stored posts, and uses its own knowledge for the rest.
  - Enable: on by default
  - Settings: `OpenAIChatModel` (`gpt-6.1-terra`), `ChatMaxMessages` (20), `ChatMaxOutputTokens` (800), `ChatMaxToolCalls` (5), `ChatSearchMaxHits` (8)
  - Limits: private chat only, no web search, one conversation per digest (a new digest forgets the old one), about 2 cents for a question the digest answers and several times more when the assistant uses tools, no daily limit; see [how it works](docs/user-guide/how-it-works.md#asking-about-the-digest)

### Dependencies
- Added `Microsoft.Agents.AI` 1.28.0.

## [v2026.11.25.49-birch] - 2026-11-25

### Contract changes
- **Setting `ChatMaxOutputTokens`: default 4000, range 1-32000** (compatible)
  - Before: 800 (1-4000)
  - After: 4000, because the hidden reasoning of the chat model counts against it
  - Affects: nothing; the chat was released a few hours ago
  - Migration: none

### Added
- **`ChatReasoningEffort` setting**: how much the chat model thinks before answering, `none`, `low`, `medium` (default), `high`.
  - Enable: on by default (`medium`)
  - Settings: `ChatReasoningEffort` (`medium`)
  - Limits: a question costs about 2 to 4 cents; `none` is the fallback if the chat fails after a model change

### Fixed
- Fixed the chat answering «Не вдалося відповісти» to every question: OpenAI rejects function tools together with reasoning on Chat Completions (HTTP 400). The chat now uses the Responses API without stored output ([ADR-0011](docs/adr/0011-call-the-chat-model-through-the-responses-api.md)).

## [v2026.11.25.44-lilac] - 2026-11-25

### Added
- **Warning about unscored posts**: when posts are still unscored two hours after publication (scoring failed, for example an expired OpenAI key), the digest is followed by «⚠️ Не оцінено N постів» with a hint to check `to-score-poison` and Application Insights. When nothing was scored at all, this message replaces «За добу нових постів не було», which used to make a broken scorer look like a quiet day.
  - Enable: on by default
  - Settings: none
  - Limits: the unscored posts are not retried; see [troubleshooting](docs/user-guide/troubleshooting.md)

### Improved
- WTelegramClient logs only warnings and errors, so routine connection messages no longer use up the daily log quota.

### Fixed
- Fixed an empty digest when the model named only unknown post ids: its posts were still marked as sent and lost. The digest now falls back to the plain list with the scoring reasons, as when OpenAI is down.
- Fixed a manual `collect` (run-now) that overlapped the hourly collection using the Telegram session twice at once, which can make Telegram revoke the session. The second collection is now skipped with a warning in the log.

## [v2026.11.24.43-plover] - 2026-11-24

### Logic
- **Digest text: one entry per event, no guesses about demand**
  - Before: the same event reported by two channels got two entries, and «Чому важливо» sometimes guessed how a post could affect the catalog or demand
  - After: one entry per event, with a note that other channels wrote about it too; «Чому важливо» must rest on what the post says, and without a basis for a link to the marketplace it says only what the news means for the SEO market
  - Effect: a shorter digest without repeated news and with fewer speculative remarks

## [v2026.11.24.42-robin] - 2026-11-24

### Logic
- **Telegram sends: no automatic repeat, 30 seconds per attempt**
  - Before: a send that timed out after 10 seconds was repeated up to three times, and each repeat could deliver the message again, because the first attempt may have reached Telegram even though its answer came too late
  - After: a send is tried once and gets 30 seconds; if it fails, the job fails and the next run sends the posts again
  - Effect: no more copies of one message from a slow answer; a failed digest is repeated by the next run, as before

### Improved
- The log shows the length and the time of every Telegram send (`Telegram message sent: N chars in M ms`, `Telegram send failed: N chars after M ms`), never the text.

## [v2026.11.24.41-kite] - 2026-11-24

### Fixed
- Fixed the new sectioned digest not reaching the model: OpenAI rejected its response schema (`reference can only point to definition`), so the digest fell back to the plain list. Each entry type is now used once in the schema, and a test checks it.

## [v2026.11.24.40-swift] - 2026-11-24

### Fixed
- Fixed `tools/run-now.cs` failing to build with `NU1903` (a vulnerable build-only compiler package): the tool no longer pulls in Native AOT, so `dotnet run tools/run-now.cs -- digest --storage <account>` works as documented.

## [v2026.11.24.40-ibis] - 2026-11-24

### Contract changes
- **Telegram digest: sections instead of one list** (breaking for anyone who parses the message)
  - Before: one message «📰 Дайджест» with an overview and up to five entries «🔥 Цікаве» / «📌 Найкраще за день», each with one 200-character sentence and a link
  - After: sections «🔥 Головне за день», «💼 Корисно для Collaborator», «👀 Варто звернути увагу», «📖 Рекомендую прочитати», «⚠️ Потребує перевірки» and «📌 Можна пропустити». Each entry shows the channel, the date, the text and a link. A long digest is split into several messages, and a continued section repeats its header
  - Affects: the reader of the digest; the digest can now arrive as 2–3 messages
  - Migration: none
- **Model response schema of the digest** (breaking for `prompts/digest.md` only)
  - Before: `overview` and `items` with `id` and `insight`
  - After: `overview`, `headlines`, `for_marketplace`, `notable`, `recommended` and `needs_verification`
  - Affects: `prompts/digest.md` and `DigestSummary` change together; a custom prompt written for the old schema stops working
  - Migration: none for the default prompt

### Added
- **«Можна пропустити» line**: the digest says how many posts were left out and why (near-duplicates and less important ones).
  - Enable: always on
  - Settings: none
  - Limits: it counts candidates from the last 26 hours that did not make it into the digest

### Logic
- **Digest size**
  - Before: `DigestSize` defaulted to 5 and could not exceed 8, the limit of one Telegram message
  - After: it defaults to 7 and can be 1–12, because the digest is split into several messages
  - Effect: more posts in the digest by default; the model may leave weak ones out, and they are counted in «Можна пропустити»

## [v2026.11.24.40-crane] - 2026-11-24

### Logic
- **Scoring: what counts as valuable**
  - Before: posts were scored against three short topics (SEO, link building, Collaborator)
  - After: the prompt describes the owner's work on the Collaborator site catalog and eight areas of interest (Google updates, link building, SEO market, AI and SEO, publishers and media, competitors, tools and analytics, practical cases). Facts and numbers raise the score, an opinion without data is capped at 6, and a connection to the owner's work counts only when the text contains it
  - Effect: the digest and alerts favor news that affects a link marketplace; more posts about the general SEO market may score lower than before
- **Digest text: why it matters**
  - Before: the overview and each insight only said what was interesting
  - After: insights keep specific numbers and names, show opinions and forecasts as the author's, and say why a post matters for the marketplace only when the connection is real; the overview is cut to 350 characters and says plainly when nothing important happened
  - Effect: shorter, more specific digest text. The message layout does not change

## [v2026.11.24.39-finch] - 2026-11-24

### Fixed
- Fixed the first collection run failing for every source with «Збір не вдався» and `Status: 404, Service returned no content`: a source with no saved position yet is now read from the start of the day instead of raising an error.

## [v2026.11.24.38-lark] - 2026-11-24

### Fixed
- Fixed the first code deploy failing with "Cannot find required .azurefunctions directory": the build artifact now keeps hidden files.

## [v2026.11.24.38-wren] - 2026-11-24

### Setup required
- **`TF_TELEGRAM_API_ID`**: moved from GitHub Variables to GitHub Secrets
  - Where: GitHub Secrets (Settings → Secrets and variables → Actions → Secrets)
  - Value: the `api_id` from my.telegram.org, the same as before
  - Apply: create the secret and delete the old variable before you run the `infra` workflow; with an empty secret the plan fails

### Security
- The Telegram `api_id` is no longer read from GitHub Variables (not masked in the logs of a public repository), and `telegram_api_id` is a `sensitive` Terraform variable.

## [v2026.11.24.38-heron] - 2026-11-24

### Setup required
- **`TF_BUDGET_ALERT_EMAIL`, `TF_TELEGRAM_PHONE`, `TF_TELEGRAM_CHAT_ID`**: moved from GitHub Variables to GitHub Secrets (`TF_TELEGRAM_CHAT_ID` is optional)
  - Where: GitHub Secrets (Settings → Secrets and variables → Actions → Secrets)
  - Value: the same values as before
  - Apply: create the secrets, delete the old variables, then run the `infra` workflow. Without the secrets the workflow fails because the two required Terraform variables are empty

### Security
- Email, phone and chat id are no longer read from GitHub Variables, whose values are not masked in the logs of a public repository, and the matching Terraform variables are `sensitive`, so the plan summary hides them.

### Dependencies
- Terraform 1.9.8 → 1.17.2 (`.terraform-version`, used by `deploy.yml` and `infra.yml`).

## [v2026.11.24.38-otter] - 2026-11-24

### Logic
- **Scoring: topics of interest**
  - Before: `scoring.md` held the placeholders `{тема 1}` to `{тема 3}`, so posts were scored against nothing
  - After: posts are scored against SEO, link building and Collaborator as a platform
  - Effect: the digest favors posts on these topics; the example posts and the "not interesting" list are gone until real ones are added
- **Prompts: language**
  - Before: `scoring.md` and `digest.md` were written in Ukrainian
  - After: both are written in English and ask the model for Ukrainian `topic`, `reason`, `overview` and `insight`
  - Effect: the digest text stays Ukrainian; wording of scores and summaries may differ slightly

## [v2026.11.24.38-pine] - 2026-11-24

### Setup required
- **Infrastructure change**: new `run-now` queue and the `Storage Queue Data Contributor` role for `deployer_object_id`
  - Where: Terraform
  - Value: none
  - Apply: run the `infra` workflow with apply (or `terraform apply`) before the code merges. The app starts without it, but `tools/run-now.cs` cannot reach Azure until the role exists
- **`ManualRunsEnabled`**: optional (default `false`)
  - Where: App Setting (`app_settings`)
  - Value: `true` only while you test a job with `tools/run-now.cs`, then `false` again
  - Apply: `az functionapp config appsettings set -g <rg> -n <function-app> --settings ManualRunsEnabled=true`

### Added
- **Start a job now**: `dotnet run tools/run-now.cs -- digest --storage <account>` puts a job name (`collect`, `digest`, `alert`, `cleanup`) into the `run-now` queue and the app runs it instead of waiting for the schedule.
  - Enable: set `ManualRunsEnabled=true`; access to the queue is controlled by Azure roles
  - Settings: `ManualRunsEnabled` (default `false`)
  - Limits: it does not wait for the result (read the function logs); a failed job is not retried; `digest` and `alert` send real Telegram messages. See [run a job now](docs/user-guide/configuration.md#run-a-job-now)

### Security
- Anyone who can write to the `run-now` queue can start a job while `ManualRunsEnabled` is `true`; it is `false` unless you turn it on, and access to the queue is by Azure role only.

## [v2026.11.24.38-raven] - 2026-11-24

### Setup required
- **`TelegramPairingKey`**: required, the app does not start without it
  - Where: Key Vault (secret)
  - Value: a random string of 16-64 characters from `A-Z a-z 0-9 _ -` (it travels in a t.me link), for example `openssl rand -hex 16`
  - Apply: `az keyvault secret set --vault-name <kv> --name TelegramPairingKey --value "$(openssl rand -hex 16)"`, before the code deploys
- **`TelegramWebhookSecret`**: required, the app does not start without it
  - Where: Key Vault (secret)
  - Value: a random token of `A-Z a-z 0-9 _ -`, for example `openssl rand -hex 32`
  - Apply: `az keyvault secret set --vault-name <kv> --name TelegramWebhookSecret --value "$(openssl rand -hex 32)"`, before the code deploys
- **Infrastructure change**: new `ChatBinding` table, two Key Vault references, and the deployer gets `Storage Table Data Contributor`
  - Where: Terraform
  - Value: none
  - Apply: run the `infra` workflow with apply (or `terraform apply`) first, then set the two secrets, then merge the code
- **Connect the chat**: call Telegram `setWebhook`, then press **Start** in the bot with your pairing key
  - Where: command line, after the code is deployed
  - Value: webhook URL `https://<function-host>/api/telegram/webhook` with the `TelegramWebhookSecret` as `secret_token`
  - Apply: [installation, step 6](docs/user-guide/installation.md#step-6-connect-your-chat). Until the chat is paired, the digest and alerts fail and their posts wait for the next run
- **`telegram_chat_id` / `TF_TELEGRAM_CHAT_ID`**: now optional (default empty)
  - Where: Terraform variable, GitHub Variable
  - Value: leave it as it is to keep sending to the same chat; clear it to bind the chat by pairing instead
  - Apply: nothing, an existing value keeps working and turns pairing off

### Contract changes
- **App Settings: `TelegramChatId` is optional** (compatible)
  - Before: required, the app did not start without it
  - After: optional; when set it overrides the paired chat, when empty the chat comes from pairing
  - Affects: nothing for an existing setup that has it set
  - Migration: none
- **Storage: new table `ChatBinding`** (compatible)
  - Before: no such table
  - After: one row (partition `chat`, row `digest`, column `ChatId`) holds the paired chat; delete the row to pair another chat
  - Affects: nothing
  - Migration: none, Terraform creates the table and the app creates it locally
- **HTTP: new anonymous endpoint `POST /api/telegram/webhook`** (compatible)
  - Before: the app had only timer and queue triggers
  - After: the bot's webhook; requests without the `X-Telegram-Bot-Api-Secret-Token` header are rejected with 401
  - Affects: nothing
  - Migration: none

### Added
- **Chat pairing**: write `/start <pairing key>` to the bot in a private chat and the agent remembers that chat for the digest and alerts. It replies «✅ Чат прив'язано. Дайджести й алерти надходитимуть сюди.»
  - Enable: on by default; needs the setup above
  - Settings: `TelegramPairingKey`, `TelegramWebhookSecret` (secrets); `TelegramChatId` stays as an override
  - Limits: one chat only, the first pairing wins; groups and channels cannot be paired; wrong keys get no reply. See [installation](docs/user-guide/installation.md#step-6-connect-your-chat)

### Security
- The webhook compares the secret token and the pairing key in constant time and never logs either. It checks the secret before reading the request body and accepts bodies up to 64 KB.

### Dependencies
- Added `Microsoft.Azure.Functions.Worker.Extensions.Http` 3.3.0 (the HTTP trigger).

## [v2026.11.24.38-iris] - 2026-11-24

### Setup required
- **Let `github-actions[bot]` push to `main`**: required for the changelog headings to receive the build version. The release itself works without it, and the next release renames the headings.
  - Where: repository settings, branch protection or rulesets for `main`
  - Value: allow pushes by `github-actions[bot]` (the `GITHUB_TOKEN` of the release job), without a pull request
  - Apply: before merging this change

### Contract changes
- **Release tag: `vYYYY.MM.DD.N` becomes `vYYYY.MM.DD.N-word`** (compatible)
  - Before: `v2026.11.24.131`
  - After: `v2026.11.24.131-falcon` when the merged change adds a changelog section; a push with no section keeps the plain tag
  - Affects: anything that parses release tags. The rollback input accepts both shapes
  - Migration: none, old tags keep working
- **Release notes come from `CHANGELOG.md`** (compatible)
  - Before: notes generated by GitHub
  - After: the text of the changelog section; GitHub still generates the notes when there is no section
  - Affects: readers of the GitHub Releases page
  - Migration: none

### Added
- **`CHANGELOG.md`**: a changelog for operators, written in each pull request by the `write-changelog` skill, in categories with templates.
  - Enable: on by default
  - Settings: none
  - Limits: only changes that alter behavior, a contract or a setting are listed
