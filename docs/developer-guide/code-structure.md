# Code structure

**Audience:** developers about to change or extend the code. Big picture first: [architecture](architecture.md).
The coding rules (nullable, sealed classes, cancellation tokens, logging) are in the root `CLAUDE.md` and in `src/PostAgent.Functions/CLAUDE.md`.

## Principle: organize by feature

Each folder holds everything for one capability: the function, its logic, its interfaces and its service registration.
The adapters of a feature (Telegram, Azure, the model) live in its `Infrastructure/` subfolder. Whatever several features share lives in `Shared/`,
and `Shared/` never depends on a feature (an architecture test enforces this). The namespace matches the folder path, so dependencies between features are visible from `using`.

## Repository layout

```
src/PostAgent.Functions/
├── Collecting/            collecting posts from Telegram (CollectorFunction loops over sources, SourceCollector collects one)
│   └── Infrastructure/    Telegram/ (channel and group reader, WTelegramClient, session in Blob, collection lock), source read cursors
├── Scoring/               scoring posts
│   └── Infrastructure/    model call, to-score queue
├── Digest/                daily digest
│   └── Infrastructure/    AI summary and its response schema
├── Chat/                  questions about the digest (ChatInbox: checks and queues, ChatProcessor: ask, save, send, ChatFunction: the queue trigger)
│   └── Infrastructure/    agent (Microsoft Agent Framework), tools (post tools, SettingsTools: propose-only), history cap, queue, conversation blob; SettingsProposalSender sends a plan with its buttons
├── Alerts/                alerts about bursts in groups (AlertJob holds the logic, AlertFunction is the timer)
├── RunNow/                RunNowFunction: starts any IJob by name from the run-now queue
├── SettingsMenu/          /settings: SettingsInbox (checks the chat and replays), MenuController (button and typed answer to the next screen), MenuScreens (what it looks like), CallbackData (the screen in the button)
├── Pairing/               the bot webhook that binds the chat (PairingFunction, PairingWebhook: auth and parsing, PairingService: the rules)
├── Cleanup/               cleanup of old data
├── Shared/                shared by features
│   ├── Posts/             post model, IPostRepository (Storage/: Posts table)
│   ├── Notifications/     sending to Telegram (ITelegramNotifier, ITelegramPanel: buttons and edits), ChatResolver and IChatBindingStore: which chat (Telegram/: Telegram.Bot, ChatBinding table)
│   ├── Chat/              the digest snapshot and the conversation state, IChatStateStore (written by Digest, read by Chat, cleaned by Cleanup)
│   ├── Ai/                IChatClient, prompts
│   ├── Jobs/              IJob, the contract a timer function and RunNowFunction share
│   ├── Settings/          AgentOptions, validator, parsers of settings (SourceNames, QuietHours); SettingsCatalog (what the bot may change), IEffectiveOptions and SettingsEditor over the overrides blob, SettingsProposal and SettingsProposalMessage (the agent's plan and its text, ADR-0013)
│   ├── Infrastructure/    Table, Queue and Blob clients, StorageNames (every storage resource)
│   └── Common/            small extensions
├── Program.cs
└── ServiceCollectionExtensions.cs   composes the app from all features
src/TgLogin/               one-off Telegram sign-in
tools/                     run-now.cs, a file-based app (`dotnet run tools/run-now.cs`) that starts a job through the queue
tests/PostAgent.Tests/     xUnit, same folders; Fakes/ holds the fake dependencies for function tests
Directory.Packages.props   versions of all NuGet packages in one place
infra/                     Terraform: storage, keyvault, function, monitoring, roles, budget
                           (main.tf: resource group and names; variables, outputs, providers)
prompts/                   scoring.md (scoring), digest.md (digest summary) and chat.md (chat about the digest)
docs/                      this documentation (user-guide, developer-guide, adr)
.claude/                   project skills and the commit guard hook
.github/workflows/         deploy.yml (format, build, test, code deploy), infra.yml (manual terraform plan/apply)
.editorconfig              code style and formatting rules (dotnet format)
.terraform-version         Terraform version used by deploy.yml and infra.yml
```

## Libraries instead of custom code

| Task | What is used |
|---|---|
| Model calls, answers in a strict schema | `Microsoft.Extensions.AI` (`IChatClient`) with the OpenAI adapter; the schema is built from C# types, `strict: true` is enabled by `ChatClientExtensions.AskAsync` |
| Telegram Bot API | `Telegram.Bot` |
| Reading channels and groups (MTProto) | `WTelegramClient` |
| Table, Queue, Blob, authentication | `Azure.Data.Tables`, `Azure.Storage.Queues`, `Azure.Storage.Blobs`, `Azure.Identity` |
| HTTP retries and timeouts | `Microsoft.Extensions.Http.Resilience` |
| Settings validation | `Options` with DataAnnotations and `ValidateOnStart` |

To replace the model or the provider, register `IChatClient` differently in `Program.cs`: the scoring and summary code does not know about the provider.
Package versions live only in `Directory.Packages.props`.

## Common changes

**A new feature.** Create a folder with the function, logic and interfaces, put adapters in its subfolders, add an `AddXxx()` for its services
and call it in `ServiceCollectionExtensions.AddPostAgent`.

**A new scheduled job.** Put the logic in a class implementing `IJob` (`Name` is the lower-case name, `RunAsync` the work), register it with `AddJob<T>()` in the feature's `AddXxx()`,
and add a timer function that only calls `job.RunAsync(ct)`. `RunNowFunction` finds it by `Name` with no other change; add the name to `tools/run-now.cs` (a test checks that every job name is there).

**A new cleanup.** Add one class implementing `ICleanupTask` in `Cleanup/` and register it in `Cleanup/CleanupServices.cs`. `CleanupFunction` runs it with the others.
Cleaning another table, queue or blob needs nothing else.

**A new setting.** Add the property (with its default) to `Shared/Settings/AgentOptions.cs` and its validation, then update the table in
[configuration](../user-guide/configuration.md) and `local.settings.json.example`. If it must be set from Terraform, see the rules in `infra/CLAUDE.md`.

## Next

[Local development](local-development.md): build, run and test.
