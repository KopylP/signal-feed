# Changelog

What changed in Signal Feed and what the operator must do about it. Newest first.
This is not a commit log: only changes that alter behavior, a contract or a setting are listed.

Each pull request adds one section named by a random word, `## [falcon] - 2026-11-24`. After the deploy the pipeline puts the build version in front of the word:
`## [v2026.11.24.131-falcon] - 2026-11-24`, and uses the section as the release notes (ADR-0007).
The structure, categories and templates are in the `write-changelog` skill (`.claude/skills/write-changelog/SKILL.md`).
Categories, in order: Setup required, Contract changes, Added, Logic, Improved, Fixed, Security, Removed / Deprecated, Dependencies.

## [heron] - 2026-11-24

### Setup required
- **`TF_BUDGET_ALERT_EMAIL`, `TF_TELEGRAM_PHONE`, `TF_TELEGRAM_CHAT_ID`**: moved from GitHub Variables to GitHub Secrets (`TF_TELEGRAM_CHAT_ID` is optional)
  - Where: GitHub Secrets (Settings → Secrets and variables → Actions → Secrets)
  - Value: the same values as before
  - Apply: create the secrets, delete the old variables, then run the `infra` workflow. Without the secrets the workflow fails because the two required Terraform variables are empty

### Security
- Email, phone and chat id are no longer read from GitHub Variables, whose values are not masked in the logs of a public repository, and the matching Terraform variables are `sensitive`, so the plan summary hides them.

### Dependencies
- Terraform 1.9.8 → 1.17.2 (`.terraform-version`, used by `deploy.yml` and `infra.yml`).

## [pine] - 2026-11-24

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

## [raven] - 2026-11-24

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

## [iris] - 2026-11-24

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
