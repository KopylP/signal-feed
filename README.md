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

## Settings and deployment

- [`docs/configuration.md`](docs/configuration.md): every App Setting, and how to change the schedule, sources and prompts.
- [`docs/deployment.md`](docs/deployment.md): `terraform apply`, secrets in Key Vault, TgLogin, GitHub Actions (OIDC), releases and rollback.
- [`docs/architecture.md`](docs/architecture.md): Azure resources and how they connect.

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
docs/                      architecture.md (Azure diagram), configuration.md (settings), deployment.md (deploy and CI)
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
