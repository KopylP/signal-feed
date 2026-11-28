# Code structure

**Audience:** developers about to change or extend the code. Big picture first: [architecture](architecture.md).
The coding rules (nullable, sealed classes, cancellation tokens, logging) are in the root `CLAUDE.md`, in `src/PostAgent.Core/CLAUDE.md` (features) and in `src/PostAgent.Functions/CLAUDE.md` (the host).

## Two projects

- **`PostAgent.Core`** is the agent: the features (handlers, jobs, logic, formatting, adapters) and `Shared/`. It has no Azure Functions package, so logic cannot depend on the host and is tested without it.
- **`PostAgent.Functions`** is the host: `Program`, `host.json` and the `[Function]` classes, thin, each in the folder of its feature. The worker finds functions only in its own assembly, so they cannot move to Core.
- An architecture test keeps this: no `[Function]` class in Core and no Functions host reference from Core. `TgLogin` links a few Core files and has no reference to either project.

## Principle: organize by feature

Each folder holds everything for one capability: the function, its logic, its interfaces and its service registration.
The adapters of a feature (Telegram, Azure, the model) live in its `Infrastructure/` subfolder. Whatever several features share lives in `Shared/`,
and `Shared/` never depends on a feature (an architecture test enforces this). The namespace matches the folder path, so dependencies between features are visible from `using`.

## Repository layout

```
src/PostAgent.Core/                (the function classes of a feature, e.g. Chat/ChatFunction.cs, are in src/PostAgent.Functions/<same folder>/)
├── Collecting/            collecting posts from Telegram (CollectorFunction loops over sources, SourceCollector collects one)
│   └── Infrastructure/    Telegram/ (channel and group reader, WTelegramClient, session in Blob), source read cursors
├── Deduplication/         the stage between collecting and scoring (ADR-0023, ADR-0024): PostDeduplicator (embed, drop near-duplicates, queue the rest to to-score), DuplicateFinder (no I/O), IDeduplicationLock
│   └── Infrastructure/    BlobDeduplicationLock (the lease of settings/locks/deduplication.lock, waited for)
├── Scoring/               scoring posts: ScoringStage (score, save, ask IAlertDecider), IPostScorer
│   └── Infrastructure/    model call
├── Digest/                daily digest and `/digest`: DigestInbox (a handler that only parses the period, DigestPeriod, and queues a DigestRequest), DigestRequestFunction and OnDemandDigest on the queue side (it keeps its report in IRatedDigestStore for its rating buttons)
│   └── Infrastructure/    AI summary and its response schema
├── Chat/                  questions about the digest, split by stage (ChatFunction is the queue trigger, ChatTexts the fixed replies)
│   ├── Intake/            the webhook side: ChatInbox checks a message and queues it (ChatRequest, QuestionText adds a quote or reply)
│   ├── Answering/         the queue side: ChatProcessor (ask, save, send), ChatDelivery (sends and splits), ChatReplyFormatter, SettingsProposalSender (a plan with its buttons)
│   ├── Agent/             the model side: IChatAgent and ChatAgent (Microsoft Agent Framework), ChatContext (the digest as data), ChatHistoryReducer, DataBlock, IChatDrafts and ChatDraftStream (the live preview of an answer)
│   │   └── Tools/         every tool is one class (IChatTool), found by assembly scan, built for one ChatTurn; the propose tools (invasive) only add to its ProposalPlan, the non-invasive ones (rate_post, add_watch, remove_watch, clear_context, save_memory, forget_memory) act at once (ADR-0021, ADR-0022), offer_choices sets the reply keyboard of the answer
│   ├── Memory/            the reader memory (ADR-0022): ReaderMemory (save, forget, the limit of 10, the move of the old profile), MemoryNoteFormat (file name and layout of a note), IReaderMemoryStore
│   └── Infrastructure/    Azure and Telegram adapters: the queue, the conversation blob, the reader memory blobs (BlobReaderMemoryStore) and the draft sender (TelegramChatDrafts)
├── Stats/                 /stats: StatsInbox (a handler: no model call), StatsReport (the sums), StatsMessage (the text)
├── Alerts/                alerts (ADR-0024): AlertDecider (IAlertDecider: chooses under IAlertDecisionLock, marks, publishes AlertRequest to to-alert), AlertSelector (the rules, no I/O), AlertSender (sends and marks sent; AlertSenderFunction is the queue trigger), WatchMatcher (the word match)
│   └── Infrastructure/    the to-alert queue, BlobAlertDecisionLock
├── TopicFollowing/        TopicFollower: embeds a newly followed topic once, stores its vector, asks IAlertDecider for its recent matches (FollowTopicFunction is the queue trigger)
├── RunNow/                RunNowFunction: starts any IJob by name from the run-now queue
├── SettingsMenu/          /settings: SettingsInbox (checks the chat and replays), MenuController (button and typed answer to the next screen), MenuScreens (what it looks like), MenuState (the pending question and the handled update ids), CallbackData (the screen in the button)
├── Login/                 /login (ADR-0016): LoginInbox (the command), LoginProcessor (the waiting side: phone, code, password; LoginProgress and LoginButtons hold one sign-in's step and buttons), LoginForm and LoginPage (the Mini App), WebAppInitData (checks the signature)
│   └── Infrastructure/    the attempt blob, the session blob, the tg-login queue, the WTelegram sign-in
├── Webhook/               the bot's entry point (TelegramWebhookFunction, TelegramWebhook: secret check, parsing, the handler chain)
├── Pairing/               /start <key> binds the chat (PairingInbox: a handler in the chain, StartCommand)
├── DailySummary/          the daily summary of the finished days (DailySummaryFunction is the timer, DailySummaryJob writes the summaries and queues a CleanupRequest)
├── Cleanup/               cleanup of old data, started by that request (CleanupFunction is the queue trigger, CleanupRunner runs every ICleanupTask)
├── Shared/                shared by features
│   ├── Posts/             post model, IPostRepository, IPostQueue (to-deduplicate and to-score, keyed by the queue name), PostVectors (embeddings, similarity) (Storage/: Posts table, PostQueue)
│   ├── Notifications/     sending to Telegram (ITelegramNotifier, ITelegramPanel: buttons and edits, ReplyKeyboard: tiles in place of the keyboard, TelegramMessageBuilder: packs blocks into messages), ChatResolver and IChatBindingStore: which chats (the digest goes to all of them, an answer to one; ServesAsync: is this a bound private chat) (Telegram/: Telegram.Bot, ChatBinding table)
│   ├── Cleanup/           CleanupRequest ("the summaries are written through day D") and its queue: the contract between DailySummary and Cleanup
│   ├── Stats/             the long memory: DailyStats and DailyStatsBuilder (a summary of one day), IDailyStatsStore, IUsageStore and UsageLog (tokens per day), Infrastructure/ (the blobs)
│   ├── Topics/            the followed topics' vectors (IFollowedTopicStore, Infrastructure/: settings/followed-topics.json) and the topics-to-follow queue
│   ├── Alerts/            IAlertDecider, the contract the scorer and topic following use to ask for an alert decision
│   ├── Feedback/          the votes: FeedbackItem, FeedbackLog (replace and cap), FeedbackPrompt (the scoring block), IFeedbackStore (Infrastructure/: the blob); written by the chat agent's rate_post
│   ├── Chat/              the digest snapshot and the conversation state, IChatStateStore (written by Digest, read by Chat, cleaned by Cleanup); BotCommands (the list) and BotCommandText (parses "/name@bot args")
│   ├── Ai/                IChatClient, the embedding generator (EmbedAsync), prompts
│   ├── Jobs/              IJob, the contract a timer function and RunNowFunction share
│   ├── Settings/          AgentOptions, validator, parsers of settings (SourceNames, QuietHours); SettingsCatalog (what the bot may change), IEffectiveOptions and SettingsEditor over the overrides blob (the safe write), OverridesChanges (what a change does, no I/O), SettingsProposals, SettingsProposal and SettingsProposalMessage (the agent's plans, their lifecycle and text, ADR-0013)
│   ├── TelegramSession/   ISessionLock (a renewed blob lease): a collection and a sign-in never use the session at once; ISessionCipher, EnvelopeCipher and SessionEnvelope: the session and the sign-in are encrypted with the Key Vault key (KeyVaultKeyWrapper, ADR-0017)
│   ├── Infrastructure/    Table, Queue and Blob clients, StorageNames (every storage resource), BlobLock and RenewedBlobLease (a lock shared by all instances: a renewed lease on an empty blob, one blob per protected resource), ConditionalBlobAccess (read with the ETag, write or delete only that version)
│   └── Common/            small extensions
├── Program.cs
└── ServiceCollectionExtensions.cs   composes the app from all features
src/TgLogin/               Telegram sign-in from a terminal (first installation; later /login in the bot)
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

**A new feature.** Create a folder in `PostAgent.Core` with the logic and interfaces, put adapters in its subfolders, add an `AddXxx()` for its services
and call it in `ServiceCollectionExtensions.AddPostAgent`. If it has a trigger, add the thin `[Function]` class in the same folder name of `PostAgent.Functions`.

**A new scheduled job.** Put the logic in a class implementing `IJob` (`Name` is the lower-case name, `RunAsync` the work), register it with `AddJob<T>()` in the feature's `AddXxx()`,
and add a timer function (in `PostAgent.Functions`) that only calls `job.RunAsync(ct)`. `RunNowFunction` finds it by `Name` with no other change; add the name to `tools/run-now.cs` (a test checks that every job name is there).

**A new cleanup.** Add one class implementing `ICleanupTask` in `Cleanup/` and register it in `Cleanup/CleanupServices.cs`. `CleanupFunction` runs it with the others, after `DailySummaryJob` has written the summaries (a task must not touch a day after `CleanupRequest.SummarizedThrough`).
Cleaning another table, queue or blob needs nothing else.

**A new setting.** Add the property (with its default) to `Shared/Settings/AgentOptions.cs` and its validation, then update the table in
[configuration](../user-guide/configuration.md) and `local.settings.json.example`. If it must be set from Terraform, see the rules in `infra/CLAUDE.md`.

## Next

[Local development](local-development.md): build, run and test.
