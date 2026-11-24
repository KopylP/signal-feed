# Signal Feed

A Telegram post-selection agent on Azure Functions. Every hour it reads public channels and groups,
scores the posts with OpenAI against your rules, sends a digest of the best ones once a day,
and for selected groups also alerts you right away when something is going on in the discussion.

## How it works

```
Collector (hourly) ──► Posts (Table) ──► to-score (Queue) ──► Scorer (OpenAI)
                                                                   │
                       AlertFunction (hourly, groups only) ◄───────┤
                       Digest (once a day) ◄───────────────────────┘
```

| Function | Trigger | What it does |
|---|---|---|
| `CollectorFunction` | `%CollectorSchedule%` | Reads new messages from the sources over MTProto, stores the posts, queues them in batches of `ScoringBatchSize` |
| `ScorerFunction` | queue `to-score` | Scores posts with OpenAI (Structured Outputs), writes Score, Topic, Reason, DuplicateOf |
| `AlertFunction` | `%AlertSchedule%` | Sends an alert about a burst or a very important message in the groups listed in `AlertSources` |
| `DigestFunction` | `%DigestSchedule%` | Picks the best posts, asks the model for a summary and sends the digest (always) |
| `CleanupFunction` | `%CleanupSchedule%` | Runs every `ICleanupTask`. Today that deletes days older than `RetentionDays` from `Posts` |

**Channels and groups.** Channels: every post is scored on its own. Groups: if no more than
`MaxItemsPerGroup` (5) messages arrived during a run, they are separate posts too. If there are more, the messages are split by time into
at most `MaxItemsPerGroup` bundles of roughly equal size, and each bundle is scored with one call. The faster the flow,
the bigger the bundle, while the scoring cost does not grow. In the bundle text, the messages with the most
reactions, forwards and replies come first, so the key ones are not lost. The `MinPostChars` minimum length applies to single
messages, while short replies stay in a bundle: their number is exactly what shows a burst.

**Alerts.** Only for the groups in `AlertSources`; channels never trigger alerts. An alert is sent if the score of a
message or bundle is ≥ `AlertThreshold` (9) or a bundle has ≥ `BurstMinPosts` (8) messages. We notify about one group
at most once per `AlertCooldownHours`, and at most `AlertMaxPerRun` entries per run.
During `AlertQuietHours` (for example `23:00-07:00`) no alerts are sent, and those posts go into the digest.
A post that has already been alerted is not repeated in the digest.

**Digest summary.** After the posts are chosen, `DigestSummarizer` makes one extra OpenAI call using the prompt
[`prompts/digest.md`](prompts/digest.md). The model returns a short overview of the day (what matters and which topics repeat) and, for every post,
an explanation of what is interesting about it. The model does not write links: the system adds them from the post data, so it cannot invent them.
If OpenAI is unavailable or the answer is invalid, the digest still arrives without a summary, with the short scoring reasons.

**Data cleanup.** The `Posts` table is partitioned by day, so `CleanupFunction` deletes whole days older than `RetentionDays`.
`ChannelState` (read cursors) is not touched. To clean up something new (another table, queue or blob), add a class that implements
`ICleanupTask` and register it in `Cleanup/CleanupServices.cs`: the function runs it together with the others.
The 48-hour fallback window of the digest only works within the data that remains, so with `RetentionDays = 1` it is yesterday and today.

## Settings (App Settings)

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
| `TelegramChatId`, `TelegramApiId`, `TelegramPhone` | | where to send; account data for reading |
| `SessionBlobContainer` | `tg-session` | container with the Telegram session |
| `CleanupSchedule` | `0 0 3 * * *` | cleanup CRON (UTC) |
| `RetentionDays` | 1 | how many days back to keep posts: 1 means yesterday's and today's partitions |
| `FirstRunLookbackHours` | 24 | how many hours of history to read on the first run for a new source |

**Where the defaults live.** The single source is `Shared/Settings/AgentOptions.cs`. The exception is `Sources`: its default is set
in `infra/variables.tf` (override it in `terraform.tfvars` or with the `TF_SOURCES` variable in GitHub). In Terraform, only the
required and infrastructure values get their own variables (sources, Telegram data, schedules, session container). Everything else is set through the `app_settings` variable
using the names from the table above, for example `app_settings = { DigestSize = "6", AlertQuietHours = "23:00-07:00" }`.
If a parameter is not set, the value from code applies. A typo in a key name is not an error, the parameter is simply ignored.

Settings are validated on startup: required values (sources, OpenAI key, bot token, Telegram data), ranges
(for example `DigestSize` and `AlertMaxPerRun` from 1 to 8, `ScoringBatchSize` 1–100, `MaxItemsPerGroup` 1–50, `RetentionDays` 1–365),
the `AlertQuietHours` format with a time zone, and that `AlertWindowHours + AlertCooldownHours` does not exceed `RetentionDays * 24`.
If something is wrong, the app does not start and the log names the parameter.

### How to change the schedule, sources and prompt

**Schedule.** CRON has six fields (`second minute hour day month weekday`) and is set **in UTC**.
`0 0 8 * * *` is 08:00 UTC, which is 10:00 in Kyiv in winter (UTC+2) and 11:00 in summer (UTC+3), so you have to account for daylight saving time yourself.
Schedules are read through `%CollectorSchedule%` and the like, so you can change them without rebuilding the code. Just update the
App Setting (or the Terraform variable, for example `digest_schedule`, or the GitHub Variable `TF_DIGEST_SCHEDULE`, and run `terraform apply` or the `infra` workflow), and the app restarts with the new schedule. Precedence: a schedule key in `app_settings` (`TF_APP_SETTINGS`) wins over the dedicated variable (`TF_*_SCHEDULE`), which wins over the default.

```bash
az functionapp config appsettings set -g <rg> -n <function-app> \
  --settings DigestSchedule="0 0 6 * * *" CollectorSchedule="0 0 */2 * * *"
```

**Sources.** `Sources` (and `AlertSources`) are comma-separated usernames; you do not have to strip `@` and `t.me/`:

```bash
az functionapp config appsettings set -g <rg> -n <function-app> \
  --settings Sources="channel_one,channel_two,some_group" AlertSources="some_group"
```

**Prompts.** The files [`prompts/scoring.md`](prompts/scoring.md) (scoring) and [`prompts/digest.md`](prompts/digest.md) (digest summary) are copied into the build, so a change
needs a new deploy (a push to `main` is enough when `DEPLOY_ENABLED` is on). **Before the first deploy**, fill in your own topics in `scoring.md` in place of `{тема 1}`
and examples in place of `{реальний пост...}`: otherwise the model will score against the literal placeholders.

## Running locally with Azurite

You need: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local),
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (`npm i -g azurite`).

```bash
azurite --silent --location .azurite &
cp src/PostAgent.Functions/local.settings.json.example src/PostAgent.Functions/local.settings.json
# fill in the placeholders: OpenAI key, bot token, Telegram data

# one-off Telegram sign-in (the session goes to Azurite)
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...
dotnet run --project src/TgLogin -- "UseDevelopmentStorage=true"

cd src/PostAgent.Functions && func start
```

Locally, the tables, queue and container are created automatically. To see a result right away, trigger a timer by hand:
`curl -X POST http://localhost:7071/admin/functions/CollectorFunction -H "Content-Type: application/json" -d "{}"`.

Tests: `dotnet test`.

## Deployment

You need the Azure CLI (`az login`), Terraform ≥ 1.5 and an Azure subscription.

**Before you start.** Check that Flex Consumption is available in your region:
`az functionapp list-flexconsumption-locations -o table`, and change `location` if needed.

### 1. terraform apply

```bash
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
cd infra
cp terraform.tfvars.example terraform.tfvars   # fill in, notably deployer_object_id and budget_alert_email
terraform init
terraform apply
```

`deployer_object_id`: `az ad signed-in-user show --query id -o tsv`. State is local by default,
see `backend.tf.example` for remote state.

Terraform creates: a resource group, storage (containers `deployment-package` and `tg-session`, tables `ChannelState` and `Posts`,
queue `to-score`), a Key Vault (RBAC), Log Analytics (30 days, 0.1 GB a day), Application Insights, an FC1 plan and a function app
with a managed identity and roles, and a $5 monthly budget (alerts at 80% and 100%).

### 2. Secrets in Key Vault

Terraform does not create the secret values, so they never end up in the state:

```bash
KV=$(terraform output -raw key_vault_name)
read -rsp "OpenAI API key: " V && az keyvault secret set --vault-name $KV --name OpenAIApiKey --value "$V" >/dev/null; echo
read -rsp "Telegram bot token: " V && az keyvault secret set --vault-name $KV --name TelegramBotToken --value "$V" >/dev/null; echo
read -rsp "Telegram api_hash: " V && az keyvault secret set --vault-name $KV --name TelegramApiHash --value "$V" >/dev/null; echo
```

The app reads them through Key Vault references. If a reference is not picked up, check that the function's identity has the `Key Vault Secrets User` role
and restart the app (`az functionapp restart`). The app notices an unresolved reference by itself:
startup fails with an error that names the secret.

### 3. TgLogin (Telegram sign-in)

Use a separate account. `api_id` and `api_hash` come from [my.telegram.org](https://my.telegram.org). If a group cannot be
read, join it with this account.

```bash
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...
dotnet run --project src/TgLogin -- $(terraform -chdir=infra output -raw storage_account_name)
```

The program asks for the Telegram code and the 2FA password, then uploads the session to the `tg-session` container through `DefaultAzureCredential`
(your `az login`; the Storage Blob Data Contributor role has already been granted by Terraform). If the session expires, repeat this step.

### 4. Deploying the code

```bash
dotnet publish src/PostAgent.Functions -c Release -o publish
cd publish
func azure functionapp publish $(terraform -chdir=../infra output -raw function_app_name)
```

## GitHub Actions: build, test, deploy through OIDC

Workflow: [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml). It builds and tests on a push to `main` and on every PR, and from `main` it also deploys (deploys never run at the same time) and then creates a release. It can also be started by hand (`workflow_dispatch`).

Pushes that only change `docs/**`, `README.md`, `CLAUDE.md` files, `.claude/**` or `infra/**` do not run it (the `prompts/*.md` files do, because they ship with the build).
Formatting follows [`.editorconfig`](.editorconfig). The build job of `deploy.yml` runs `dotnet format` first: on a pull request from this repository it (and `terraform fmt`) commits any fix to the PR branch as `github-actions[bot]` and builds the fixed code; on `main`, forks and Dependabot it only verifies. Run `dotnet format` locally to fix it before you push.

The same build job runs `terraform validate` on `infra/` (no backend, no Azure sign-in), so a broken Terraform change blocks the deploy too. Dependabot opens weekly grouped updates for NuGet, GitHub Actions and Terraform.

One-time OIDC setup (no passwords):

```bash
APP_ID=$(az ad app create --display-name signal-feed-deploy --query appId -o tsv)
az ad sp create --id $APP_ID
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-owner>/<repo>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
az role assignment create --assignee $APP_ID --role Contributor \
  --scope $(az functionapp show -g <rg> -n <function-app> --query id -o tsv)
```

In the GitHub repository (Settings → Secrets and variables → Actions → Variables) set:
`AZURE_CLIENT_ID` (this is `$APP_ID`), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_FUNCTIONAPP_NAME`.

### Infrastructure from GitHub Actions (manual run)

The workflow [`.github/workflows/infra.yml`](.github/workflows/infra.yml) runs only by hand (Actions → infra → Run workflow).
Without the `apply` checkbox it only runs `terraform plan` (visible in the run summary); with the checkbox it also runs `terraform apply`.
If the infrastructure does not exist, it is created; if the code in `infra/` changed, it is updated. The application code is deployed by the separate workflow
`build-test-deploy` after a push to `main`.

Because the federated credential accepts only `refs/heads/main`, the workflow can sign in to Azure only when run from `main`: merge an infrastructure change first (it deploys no code), then run `infra` from `main`.

The pipeline creates the resources itself, so its identity (App Registration) and the state storage are created **by hand once**:
they cannot be described in the same Terraform that they run.

```bash
# 1. State storage
az group create -n rg-tfstate -l westeurope
az storage account create -g rg-tfstate -n <unique-sttfstate-name> --sku Standard_LRS --allow-blob-public-access false
az storage container create --account-name <same-name> -n tfstate --auth-mode login

# 2. Pipeline identity (instead of the variant in the section above, with broader roles)
SUB=$(az account show --query id -o tsv)
APP_ID=$(az ad app create --display-name signal-feed-deploy --query appId -o tsv)
az ad sp create --id $APP_ID
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main", "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-owner>/<repo>:ref:refs/heads/main", "audiences": ["api://AzureADTokenExchange"]
}'
# create resources
az role assignment create --assignee $APP_ID --role Contributor --scope /subscriptions/$SUB
# grant roles to the function and to you (roles.tf); Contributor cannot do this
az role assignment create --assignee $APP_ID --role "Role Based Access Control Administrator" --scope /subscriptions/$SUB
# read and write the state
az role assignment create --assignee $APP_ID --role "Storage Blob Data Contributor" \
  --scope $(az storage account show -n <sttfstate-name> --query id -o tsv)
```

GitHub Variables (Settings → Secrets and variables → Actions → Variables), all non-secret:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | sign-in to Azure (OIDC) |
| `TFSTATE_RESOURCE_GROUP`, `TFSTATE_STORAGE_ACCOUNT` | state storage (`TFSTATE_CONTAINER`, default `tfstate`) |
| `TF_DEPLOYER_OBJECT_ID`, `TF_BUDGET_ALERT_EMAIL` | `deployer_object_id`, `budget_alert_email` |
| `TF_SOURCES` (optional), `TF_ALERT_SOURCES` | `sources` (default: the six SEO channels from `variables.tf`), `alert_sources` |
| `TF_TELEGRAM_CHAT_ID`, `TF_TELEGRAM_API_ID`, `TF_TELEGRAM_PHONE` | the matching Terraform variables |
| `TF_COLLECTOR_SCHEDULE`, `TF_DIGEST_SCHEDULE`, `TF_ALERT_SCHEDULE`, `TF_CLEANUP_SCHEDULE` (optional) | CRON in UTC; default: the values in the settings table; `TF_APP_SETTINGS` overrides them |
| `TF_LOCATION`, `TF_NAME_PREFIX` (optional) | default `westeurope` and `signalfeed` |
| `TF_APP_SETTINGS` (optional) | JSON, for example `{"DigestSize":"6"}`; matches `app_settings` |

The secrets (OpenAI, Telegram) are not needed in GitHub: as before, they go into Key Vault with `az keyvault secret set`.
After the first `apply`, copy `function_app_name` from the run summary into `AZURE_FUNCTIONAPP_NAME` for the code deploy.

### Releases and rollback

Every deploy from a push to `main` is a release: a `vYYYY.MM.DD.N` tag and a GitHub Release with generated notes. `YYYY.MM.DD` is the UTC build date and `N` is GitHub's
built-in run counter of `deploy.yml`, which only grows, so versions never collide (numbers have gaps because pull request builds count too). The same version is baked into the build
(`-p:Version`), so it matches the tag. No deploy (see `DEPLOY_ENABLED` below) means no release. Details: [ADR-0005](docs/adr/0005-release-every-deploy-with-a-build-counter-version.md).

**Rollback.** Actions → build-test-deploy → Run workflow, and enter the earlier release tag, for example `v2026.11.20.120`. The workflow rebuilds that tag, tests it and deploys it,
and creates no new release. Or revert the commit through a pull request, which deploys the previous code as a new release. A rollback restores code only: infrastructure,
App Settings, secrets and stored data stay as they are, and a renamed or removed required setting can stop an older version from starting.

The repository must allow Actions to create releases: the release job asks for `contents: write`, so Settings → Actions → General → Workflow permissions must not block it.

**Deploy is off by default.** Without the `DEPLOY_ENABLED` variable set to `true`, the workflow only builds and tests, and the deploy and release steps
are skipped. When you are ready to deploy, set this variable in the same place as the rest.

## Security and cost

- There are no secrets in the code or the repository. `local.settings.json` is in `.gitignore`, and `local.settings.json.example` has placeholders only.
- Storage access uses managed identity. The bot token does not reach logs or telemetry: HTTP logging of the client is off, and
  `TelegramTokenRedactor` replaces the token in Application Insights dependency records.
- OpenAI receives post texts and, for groups, author names and participant usernames. This is personal data of third parties, even though the sources are public.
- Roughly: the functions stay within the free grant of Flex Consumption, storage costs pennies, and the main expense is OpenAI.
  The $5 budget on the resource group sends an email alert at 80% and 100%.

## Ready-made solutions instead of custom code

| Task | What is used |
|---|---|
| Model calls, answers in a strict schema | `Microsoft.Extensions.AI` (`IChatClient`) with the OpenAI adapter; the schema is built from C# types, `strict: true` is enabled by `ChatClientExtensions.AskAsync` |
| Telegram Bot API | `Telegram.Bot` |
| Reading channels and groups (MTProto) | `WTelegramClient` |
| Table, Queue, Blob, authentication | `Azure.Data.Tables`, `Azure.Storage.Queues`, `Azure.Storage.Blobs`, `Azure.Identity` |
| HTTP retries and timeouts | `Microsoft.Extensions.Http.Resilience` |
| Settings validation | `Options` with DataAnnotations and `ValidateOnStart` |

To replace the model or the provider, it is enough to register `IChatClient` differently in `Program.cs`: the scoring and summary code does not know about the provider.

## Structure

The code is organized by feature: each folder holds everything for one capability (function, logic, interfaces, service registration).
The adapters of a feature (Telegram, Azure, model) live in its `Infrastructure/` subfolder, and whatever several features share lives in `Shared/`. The namespace matches
the folder path, and dependencies between features are visible from `using`.

```
src/PostAgent.Functions/
├── Collecting/            collecting posts from Telegram (CollectorFunction loops over sources, SourceCollector collects one)
│   └── Infrastructure/    Telegram/ (channel and group reader, WTelegramClient, session in Blob), source read cursors
├── Scoring/               scoring posts
│   └── Infrastructure/    model call, to-score queue
├── Digest/                daily digest
│   └── Infrastructure/    AI summary and its response schema
├── Alerts/                alerts about bursts in groups
├── Cleanup/               cleanup of old data
├── Shared/                shared by features
│   ├── Posts/             post model, IPostRepository (Storage/: Posts table)
│   ├── Notifications/     sending to Telegram (Telegram/: Telegram.Bot)
│   ├── Ai/                IChatClient, prompts
│   ├── Settings/          AgentOptions, validator, parsers of settings (SourceNames, QuietHours)
│   ├── Infrastructure/    Table, Queue and Blob clients, StorageNames (every storage resource)
│   └── Common/            small extensions
├── Program.cs
└── ServiceCollectionExtensions.cs   composes the app from all features
src/TgLogin/               one-off Telegram sign-in
tests/PostAgent.Tests/     xUnit, same folders; Fakes/ holds the fake dependencies for function tests
Directory.Packages.props   versions of all NuGet packages in one place
infra/                     Terraform: storage, keyvault, function, monitoring, roles, budget
                           (main.tf: resource group and names; variables, outputs, providers)
prompts/                   scoring.md (scoring) and digest.md (digest summary)
docs/architecture.md         Azure resources and how they connect (Mermaid diagram)
docs/adr/                  architecture decision records (why, not what)
.claude/                   project skills and the commit guard hook
.github/workflows/         deploy.yml (format, build, test, code deploy), infra.yml (manual terraform plan/apply)
.editorconfig              code style and formatting rules (dotnet format)
.terraform-version         Terraform version used by deploy.yml and infra.yml
```

**A new feature.** Create a folder with the function, logic and interfaces, put adapters in its subfolders, add `AddXxx()` for its
services and call it in `ServiceCollectionExtensions.AddPostAgent`. A new cleanup is one `ICleanupTask` class in `Cleanup/`.

## Known limitations

- **Message retries.** If Telegram answered `sendMessage` with a delay, a retry can produce a duplicate digest or alert. This is a deliberate choice in favor of not losing a message.
- **A message that could not be scored.** After 5 failed attempts it lands in the `to-score-poison` queue, and the posts stay `New`
  until cleanup. There is no notification about this, so keep an eye on the queue in Azure.
- **Collection failure.** The message «Збір не вдався» ("Collection failed") arrives every hour until the cause is fixed.
- **Bundles after a failure.** If a Collector run broke off between writing the posts and updating the cursor, and new messages arrived in the meantime, the same group messages can end up in two different bundles.
- **First run.** For a busy group, reading a day of history means many requests in a row, so FLOOD_WAIT is possible. The source is then skipped until the next run.
- **Fresh messages** have not collected reactions yet, so the prominence order in a bundle is approximate at first.
- **The "sent" mark.** If the message went out but writing the status to the table failed, the next run can repeat the alert.
- **Storage tests** are not part of `dotnet test`: `PostRepository` is checked by hand on Azurite.
