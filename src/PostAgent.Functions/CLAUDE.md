# src/PostAgent.Functions: Azure Functions

General C# rules are in the root `CLAUDE.md`. Feature structure: `docs/developer-guide/code-structure.md`.

## Features and registration

- A feature is a folder. Its root holds the function, `XxxServices`, pure logic, message formatting, models and interfaces. `Infrastructure/` holds the adapters (Telegram, Azure, model) and the response schemas the adapter produces. A feature with no adapters (`Alerts/`, `Cleanup/`) has no `Infrastructure/`.
- One type per file; the file name is the type name.
- A large feature may be split by stage into subfolders named for what they do (`Chat/` has `Intake/`, `Answering/`, `Agent/`, with `Infrastructure/` for the Azure adapters); the namespace follows the folder, and a stage never reaches into another's internals.
- `Shared/` never references a feature. A name or type that both need (a storage resource name, a settings parser, `PostScore`) lives in `Shared/`.
- Each feature has `XxxServices.AddXxx(this IServiceCollection)`, called from `ServiceCollectionExtensions.AddPostAgent`.
- A function stays thin: it takes dependencies through the constructor, calls the logic and catches only what it can handle. Pure logic (selection, formatting) lives in static classes and is tested on its own.
- A scheduled job is an `IJob` class (`Shared/Jobs`) registered with `AddJob<T>()`; its timer function only calls `RunAsync`. `RunNowFunction` starts any job by `Name`, so never copy job logic into a second trigger. A new job name also goes into `tools/run-now.cs`.
- A new cleanup is an `ICleanupTask` class in `Cleanup/`, registered in `CleanupServices`.

## Settings

- All App Settings are read through `AgentOptions` (`Shared/Settings`). Defaults live only there, it is the single source.
- Ranges and required values use DataAnnotations; more complex checks go in `AgentOptionsValidator`. Settings are validated on startup (`ValidateOnStart`).
- Schedules are not part of `AgentOptions`: the `[TimerTrigger("%Name%")]` attributes read them directly.
- When you add or change a parameter, update the table in `docs/user-guide/configuration.md` and `local.settings.json.example`; for Azure see `infra/CLAUDE.md`.
- Secrets (`OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash`, `TelegramPairingKey`, `TelegramWebhookSecret`) come from Key Vault. Never log them; `TelegramTokenRedactor` strips the bot token from telemetry.

## Public endpoint

- `TelegramWebhookFunction` is anonymous because Telegram cannot send a function key. Every check lives in `TelegramWebhook`: the secret token header first, compared with `SecretComparison` (constant time), before the body is read (the body is passed as a `Stream` for that reason, capped at 64 KB).
- The `/login` Mini App (`LoginPage`, `LoginSubmit`, ADR-0016) is anonymous too: `LoginForm` trusts only `WebAppInitData` (HMAC with the bot token, fresh `auth_date`), then the attempt id (constant time), the chat that started it and the binding. Never log the phone, a code or `initData`; the waiting `LoginProcessor` clears them from the attempt blob as soon as it reads them and never rethrows.
- The 2FA password never reaches the server: the page computes the SRP proof (`LoginSrp`, kept in step with `LoginSrpTests`) and posts only `srpA`/`srpM1`. Never add a password field to the form, the attempt or a log; `WTelegramUserLogin` calls `auth.*` through `Invoke` because WTelegram's login dialogue needs the password.
- Every read and write of the session blob and of `login-attempt.json` goes through `ISessionCipher` (envelope with the Key Vault key `SessionKeyId`, the blob name as purpose, ADR-0017). Never write either in clear; `UnencryptedLocalCipher` exists only for Azurite. A key id read from a blob is accepted only if it is a version of `SessionKeyId`.
- A collection and a sign-in share the Telegram session only through `ISessionLock` (`Shared/TelegramSession`). The collector accepts whichever account owns the session (`user_id` = `-1`) and never asks Telegram for a code. A replaced session is announced to every paired chat (any of them may sign in any account).
- Never reply to a wrong key and never log a key or the secret. Every chat with the right key is added up to `TelegramMaxChats`; `TelegramChatId`, when set, overrides the stored chats and turns pairing off (ADR-0008, ADR-0014).

## Chat about the digest (ADR-0010)

- `TelegramWebhook` does no work of its own: it offers every update to the `IIncomingMessageHandler` list in registration order, the first that returns true wins (`Shared/Notifications`; `PairingInbox` is registered first, the settings menu before the chat). A new bot command is a new handler, never a branch in the webhook; `Pairing`, `Digest`, `Chat` and `Cleanup` never reference each other, they share only types in `Shared/` (`Shared/Chat`).
- A chat works before the first digest: `ChatInbox` starts an empty conversation (`ReportSnapshot.None`, no report id) with `TryStartAsync`, which never replaces a digest; `Digest` replaces it with the real one. `/clear` (`ClearInbox`) and the agent's `propose_clear_context` (applied by `SettingsEditor` after the owner's tap) drop only the session, never the snapshot.
- The conversation is one blob per chat (`chat-state/<chat id>.json`, ADR-0014): `Digest` overwrites each with a snapshot after the digest is sent, `Cleanup` deletes the stale ones. Keep it that way: no history of old conversations.
- A message to every chat goes through `ITelegramNotifier.SendAsync` (one failing chat does not stop the others); an answer or a reply goes through `SendToAsync` to the chat that asked. Never read "the" chat from `ChatResolver`: ask for the list, or check `IsBoundAsync`.
- `ChatProcessor` order is ask the model, save with the blob ETag, then send. Never send before the save succeeds. While the model writes, `ChatAgent` shows a draft (`ChatDraftStream`, throttled, plain text, `ChatStreamEnabled`); a draft is only a preview (kept alive every 20 s, short budget, no retry), so its failure never fails or slows the answer and the real message is still sent by `ChatDelivery`. The `OpenAI` package must match the version that `Microsoft.Extensions.AI.OpenAI` was built against, and `ChatStreamingRequestTests` runs the real client over a stream. A turn whose digest changed is dropped, not redelivered.
- Dedupe handled updates by the set in `ChatState.ProcessedUpdateIds`, never by a watermark. A chat queue message that cannot be parsed is dropped, not retried.
- Anything a channel wrote (the snapshot, tool results) goes to the model through `DataBlock.Wrap`, never into the instructions. A tool gets a cap on its output (`ChatSearchMaxHits`).
- `ChatMaxToolCalls`, `ChatMaxOutputTokens` and `ChatMaxMessages` bound the cost of a turn; the chat model is `OpenAIChatModel` (keyed `IChatClient`, `AiServices.ChatClientKey`), scoring and the digest use `OpenAIModel`.
- The chat client uses the OpenAI Responses API with `store=false` and encrypted reasoning (ADR-0011). Chat Completions rejects tools together with reasoning, and a stored response makes the agent framework refuse our own history. `ChatModelRequestTests` checks the real request; keep it green when the client or the agent changes.

## Settings from Telegram (ADR-0012)

- A parameter the bot may change goes into `SettingsCatalog` (ranges come from the `Range` attributes); secrets, schedules and infrastructure values never do.
- A job reads `IEffectiveOptions.GetAsync` once at the start of a run and passes the snapshot down; never hold `AgentOptions` in a singleton field. Secrets and infrastructure values keep `IOptions<AgentOptions>`.
- Every write to `settings/overrides.json` goes through `SettingsEditor` (ETag, up to 3 retries, validated as a whole); reset is never refused. A button's `callback_data` is at most 64 bytes and carries the target value, not a step.
- The menu answers only bound private chats (the question it waits for and a proposal belong to one chat and are not touched by another) and records the ids of the updates it claimed (the last 30, a set, never a watermark: ids can restart and arrive out of order); a resent one is claimed and ignored, an ordinary chat question is never recorded. A blob that cannot be read is never overwritten except by "reset all". Models are chosen from a fixed list, the model is set per request by `ModelSelectingChatClient`.

- Every tool of the chat agent (posts, settings, prompts, profile) is one class in `Chat/Agent/Tools/` that implements `IChatTool` (`Build(plan)` returns the `AIFunction`, parameter descriptions on the lambda). Every such class in the assembly is registered by `AddChatTools`: a new tool is a new file, nothing to register or list. A propose tool only calls `ProposalPlan.AddAsync`.
- Prompts reach the model through `IPromptProvider` (never `Prompts.Load` in a job): the file with the owner's edits of its editable sections (`PromptSections` in the overrides, ADR-0015). The reader profile is its own blob per chat (`IProfileStore`, keyed by chat id), added to the chat instructions inside `<about_me>`; a text edit (`EditPrompt`, `EditProfile`) is a fragment replaced by another, checked by `TextEdit`, shown by code as before and after, and a profile edit is a plan of its own.
- The chat agent changes settings only through its propose tools (ADR-0013): they build a plan of `SettingChange` and never write. The plan is stored and shown by code (`SettingsProposalSender`, `SettingsProposalMessage`), applied only by the button (`SettingsEditor.ApplyProposalAsync`, one checked write). Never put model text into that message, never add an apply tool.

## Data and queues

- `run-now` messages start jobs only while `ManualRunsEnabled` is true (off by default in Azure) and are not retried: a failure is logged, not rethrown.
- The `Posts` table is partitioned by day (`PartitionKey` = date). Changing the `PostEntity` schema affects data that is already stored: make new fields optional.
- A `to-score` queue message is processed idempotently: a redelivery must not create duplicates.
- Model calls go through `IChatClient` with strict schemas built from C# types (`ChatClientExtensions.AskAsync`). Post text is data, not instructions.
- The digest model's entries refer to posts by `id` and carry only text; the channel, date and link come from the stored post (`DigestReport`, `DigestMessage`). The digest is several messages,
  and the posts are marked as sent after the last one (ADR-0009).
- A Telegram send is a POST and is never retried by the HTTP pipeline (`NotificationsServices.ConfigureTelegramResilience`): a repeat after a timeout duplicates a delivered message. Log the length and time of a send, never its text.

## Do not forget

- A new NuGet package: version in `Directory.Packages.props`, package in `.csproj` without a version.
- A change in a function's behavior needs a test in `tests/PostAgent.Tests/`, in the matching folder.
