# Changelog

What changed in Signal Feed and what the operator must do about it. Newest first.
This is not a commit log: only changes that alter behavior, a contract or a setting are listed.

Each pull request adds one section named by a random word, `## [falcon] - 2026-11-24`. After the deploy the pipeline puts the build version in front of the word:
`## [v2026.11.24.131-falcon] - 2026-11-24`, and uses the section as the release notes (ADR-0007).
The structure, categories and templates are in the `write-changelog` skill (`.claude/skills/write-changelog/SKILL.md`).
Categories, in order: Setup required, Contract changes, Added, Logic, Improved, Fixed, Security, Removed / Deprecated, Dependencies.

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
