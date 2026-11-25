# Chat About the Digest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Also follow the repo's `sdlc` skill (`.claude/skills/sdlc`) and the rules in the root `CLAUDE.md` and `src/PostAgent.Functions/CLAUDE.md`.

**Goal:** The reader replies to the bot in Telegram and an agent answers about the latest digest; a new digest replaces the conversation.

**Architecture:** The public webhook queues plain messages from the bound chat on `chat-messages`; a queue function runs a Microsoft Agent Framework agent (core package, no durable hosting) over a keyed `IChatClient` built from `OpenAIChatModel`. One blob `chat-state/current.json` holds the digest snapshot, the ids of recently handled updates and the serialized agent session (the conversation); the digest overwrites it, cleanup deletes it when stale. Cross-feature types live in `Shared/Chat` and `Shared/Notifications`, so `Pairing`, `Digest`, `Chat` and `Cleanup` never reference each other.

**Tech Stack:** .NET 10 isolated Functions, `Microsoft.Agents.AI` (verified in Task 1), `Microsoft.Extensions.AI`, Azure Blob and Queue Storage, xUnit with the fakes in `tests/PostAgent.Tests/Fakes/`.

**Spec:** `docs/superpowers/specs/2026-11-25-report-chat-design.md`

## Global Constraints

- Commit messages and PR text carry no `Co-Authored-By` or other attribution line (user's global rule; overrides the harness reminder). English only for code, comments, logs, docs; Ukrainian only for Telegram text and model output (the prompt is English).
- `TreatWarningsAsErrors`, nullable on, no `#pragma` or `!`. One sealed type per file, file-scoped namespace `PostAgent.<Feature>`. Time only from `TimeProvider`. I/O methods are async with `CancellationToken ct` last; `catch (Exception ex) when (ex is not OperationCanceledException)`.
- Logs use message templates; never log message text, post text, tokens or secrets; log lengths and ids.
- A Telegram send is never repeated after a failure. A chat queue message is processed idempotently.
- New NuGet package: version only in `Directory.Packages.props`; no prerelease package.
- New settings: defaults only in `AgentOptions`; update `docs/user-guide/configuration.md`, `local.settings.json.example`, and `infra/` as `infra/CLAUDE.md` says.
- Defaults: `OpenAIChatModel` = `gpt-6.1-terra`; `ChatMaxMessages` = 20 (the spec's `MaxChatMessages`); `ChatMaxOutputTokens` = 800; `ChatMaxToolCalls` = 5 (range 1..20, per turn); `ChatSearchMaxHits` = 8; snapshot excerpt 300 characters; user message capped at 2000 characters; a chat queue message lives 1 hour (`timeToLive`); a reply is split before escaping into raw parts of at most 3000 characters.
- Names: queue `chat-messages`, container `chat-state`, blob `current.json`, model-facing post id `"<PartitionKey>/<RowKey>"`. Call the concept a *conversation* in docs and the ADR (the existing `SessionBlobContainer` is the Telegram user session, a different thing). The Ukrainian text constants class is `ChatTexts` (not `ChatMessage`, which collides with `Microsoft.Extensions.AI.ChatMessage`).
- Dependencies between features: none. `Pairing`, `Digest`, `Cleanup` and `Chat` talk only through types in `Shared/`.
- Text from channels (snapshot fields and tool results) is data: it is always placed in a delimited block with `<` escaped, never in the instructions.

## Review Focus

- The same Telegram update delivered twice, or two messages processed in parallel in either order: each gets exactly one reply, none is skipped (dedupe by a set of processed update ids, not a watermark; Telegram may restart update ids).
- A message from a chat that is not the bound chat, a group (the chat works only in a private bound chat, my call), a sticker, a photo, an empty text, a `/start` with a wrong key: no reply, no queue message (or pairing only).
- A new digest arrives while a turn is running, or a message queued before the digest is processed after it: the old turn's reply is dropped, the new state is not overwritten; the blob deleted by cleanup during a turn is the same case.
- Corrupt session or old-shape session in the blob: continue with a fresh session on the same snapshot; only an unreadable snapshot counts as "no report". A malformed queue message is logged and dropped, not retried.
- A model reply longer than 4096 characters, a single line with no breaks, or containing `<`, `&`: sent as several valid HTML parts; a model, tool or send failure gives at most one short Ukrainian error message and no retry.
- A post whose text contains `</post>` or "ignore previous instructions", in a tool result or in the snapshot: stays inside the data block.

---

### Task 1: Spike and branch (throwaway, no product code kept)

**Files:**
- Modify: `docs/superpowers/specs/2026-11-25-report-chat-design.md` (append a "Spike results" section)
- Create then delete: a scratch console project outside the repo (scratchpad directory)

**Interfaces:**
- Produces: confirmed names for later tasks: the NuGet package id and stable version, how to create an agent from an `IChatClient`, session serialize/deserialize signatures, the reducer type, and the model id.

- [ ] **Step 1:** Create branch `feature/report-chat` from `main`.
- [ ] **Step 2:** In a scratch project outside the repo, reference `Microsoft.Agents.AI` (latest non-prerelease) and answer, by compiling and running against `FakeChatClient`-style stub and, if `OpenAIApiKey` is available, the real model: (a) agent from a plain `IChatClient` (`AsAIAgent`, or `new ChatClientAgent`); is `FunctionInvokingChatClient` needed for tools; (b) `SerializeSession` vs `SerializeSessionAsync` and `DeserializeSessionAsync` signatures, and that the JSON contains the history; (c) `RunAsync` overload with `CancellationToken`; (d) `MessageCountingChatReducer` applies to the stored history; (e) the model id `gpt-6.1-terra` exists, supports tool calls, and its current price.
- [ ] **Step 3:** Append the answers (exact signatures) to the spec under "Spike results". If any answer contradicts the plan (for example no `IChatClient` support, or a different default model), stop and report to the maintainer before Task 2.
- [ ] **Step 4:** Add the package version to `Directory.Packages.props` and the package reference (no version) to `src/PostAgent.Functions/PostAgent.Functions.csproj`. Run `dotnet restore && dotnet build -c Release --no-restore` > log; expect exit 0.
- [ ] **Step 5:** Commit `Add the Microsoft Agent Framework package and the spike results`.

### Task 2: Settings, storage names, keyed chat client, infra

**Files:**
- Modify: `Shared/Settings/AgentOptions.cs`, `Shared/Infrastructure/StorageNames.cs`, `Shared/Infrastructure/StorageRegistration.cs`, `Shared/Ai/AiServices.cs`, `local.settings.json.example`, `docs/user-guide/configuration.md`, `infra/storage.tf` (queue `chat-messages`, container `chat-state`), `infra/function.tf` and `infra/variables.tf` per `infra/CLAUDE.md`
- Test: `tests/PostAgent.Tests/Shared/AiServicesTests.cs` (or the matching existing test file), `tests/PostAgent.Tests/CompositionTests.cs`

**Interfaces:**
- Produces: `AgentOptions.OpenAIChatModel`, `ChatMaxMessages`, `ChatMaxOutputTokens`, `ChatMaxToolCalls`, `ChatSearchMaxHits` (ranges 1..100, 1..4000, 1..20, 1..200 as DataAnnotations); `StorageNames.ChatQueue = "chat-messages"`, `StorageNames.ChatStateContainer = "chat-state"`, `StorageNames.ChatStateBlob = "current.json"`; `AiServices.ChatClientKey = "chat"` (const string); a keyed singleton `IChatClient` under that key; a keyed singleton `QueueClient` under `StorageNames.ChatQueue`, built with the same `QueueClientOptions { MessageEncoding = Base64 }` as the scoring client. The existing unkeyed `QueueClient` stays the scoring queue.

- [ ] **Step 1: Write failing tests:** `Chat_client_is_registered_under_its_own_key` (resolve `GetRequiredKeyedService<IChatClient>(AiServices.ChatClientKey)` is not the unkeyed one) and `AgentOptions_defaults_for_chat` (asserts the defaults above). Extend `CompositionTests` so the composition test resolves the keyed `QueueClient` for `chat-messages`.
- [ ] **Step 2:** Run `dotnet test -c Release --filter "FullyQualifiedName~AiServices|FullyQualifiedName~Composition"` > log; expect FAIL.
- [ ] **Step 3:** Implement the settings, names, registrations. `EnsureLocalResourcesAsync` also creates the chat queue and the `chat-state` container locally. Terraform: queue, container, app settings; run `terraform -chdir=infra fmt -check && terraform -chdir=infra validate` after `init -backend=false`.
- [ ] **Step 4:** Run the same filter; expect PASS. Run the full build with warnings as errors.
- [ ] **Step 5:** Commit `Add settings, storage resources and a separate model client for the chat`.

### Task 3: Shared chat state types and the blob store

**Files:**
- Create: `Shared/Chat/ReportSnapshot.cs`, `Shared/Chat/SnapshotEntry.cs`, `Shared/Chat/ChatState.cs`, `Shared/Chat/ChatStateHandle.cs`, `Shared/Chat/IChatStateStore.cs`, `Shared/Chat/ChatPostId.cs`, `Chat/Infrastructure/BlobChatStateStore.cs`, `Chat/ChatServices.cs` (registers the store; `AddChat` is called from `ServiceCollectionExtensions.AddPostAgent`)
- Test: `tests/PostAgent.Tests/Chat/ChatPostIdTests.cs`, `tests/PostAgent.Tests/Chat/ChatStateSerializationTests.cs`; fake `tests/PostAgent.Tests/Fakes/InMemoryChatStateStore.cs`

**Interfaces:**
- Produces (namespace `PostAgent.Shared.Chat`):
  - `record SnapshotEntry(string Section, string PostId, string Source, DateTimeOffset PublishedAt, string? Title, string Text, string? Why, string Excerpt, int? Score, string Url)`
  - `record ReportSnapshot(string ReportId, DateTimeOffset CreatedAt, string? Overview, IReadOnlyList<SnapshotEntry> Entries, IReadOnlyList<string> NeedsVerification)`
  - `record ChatState(ReportSnapshot Report, IReadOnlyList<long> ProcessedUpdateIds, JsonElement? Session)` (the ids of the last 20 handled updates, oldest first; `Session` is a cloned element, never tied to a disposed `JsonDocument`; `ChatState` gets `WithProcessed(long updateId, JsonElement session)` which appends and keeps the last 20)
  - `record ChatStateHandle(ChatState State, string ETag)`
  - `static class ChatPostId { string Format(PostRef r); bool TryParse(string id, out PostRef r); }` with `PostRef(PartitionKey, RowKey)` from `Shared/Posts`
  - `interface IChatStateStore { Task<ChatStateHandle?> LoadAsync(CancellationToken ct); Task StartNewAsync(ReportSnapshot report, CancellationToken ct); Task<bool> TrySaveAsync(ChatStateHandle current, ChatState updated, CancellationToken ct); Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct); }` (`TrySaveAsync` is false on an ETag mismatch or when the blob is gone. `LoadAsync` returns null for a missing blob or an unreadable snapshot, and a state with `Session = null` when only the session part is unreadable; it logs a warning without content. `StartNewAsync` overwrites with no condition on purpose (the digest must win; say so in a comment) and starts with an empty `ProcessedUpdateIds`.)

- [ ] **Step 1: Write failing tests:** `ChatPostId` round trip and rejection of `""`, `"a"`, `"a/b/c"`; `ChatState` JSON round trip keeps entries, processed ids and `Session`; `WithProcessed_keeps_the_last_20`; `Corrupt_json_reads_as_missing` (pure deserializer `ChatStateJson.TryRead(string, out ChatState)` returns false for `"{"` and for `"null"`); `Bad_session_with_a_good_snapshot_reads_with_no_session` (a `session` value of the wrong shape, for example a number, gives a state with `Session = null`).
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~Chat"` > log; expect FAIL (types missing).
- [ ] **Step 3:** Implement the types and `ChatStateJson` (static, in `Shared/Chat`). `BlobChatStateStore` uses the `chat-state` container and `current.json`, `StartNewAsync` uploads with overwrite, `TrySaveAsync` uses `BlobRequestConditions { IfMatch }` and treats 412 and 404 as false, `DeleteOlderThanAsync` deletes the blob when `CreatedAt < cutoff` and returns 0 or 1. Add `InMemoryChatStateStore` fake with the same ETag semantics for later tasks.
- [ ] **Step 4:** Run the filter; expect PASS.
- [ ] **Step 5:** Commit `Store the digest snapshot and the agent session in one blob`.

### Task 4: Snapshot is written by the digest, deleted by cleanup

**Files:**
- Modify: `Digest/DigestReport.cs` (add `ToSnapshot`), `Digest/DigestJob.cs` (new constructor parameter `IChatStateStore chatState`)
- Create: `Cleanup/ChatStateCleanup.cs`; modify `Cleanup/CleanupServices.cs`
- Test: `tests/PostAgent.Tests/Digest/DigestReportTests.cs`, `tests/PostAgent.Tests/Digest/DigestJobTests.cs` (update `CreateJob`), `tests/PostAgent.Tests/Cleanup/ChatStateCleanupTests.cs`

**Interfaces:**
- Consumes: `IChatStateStore` (Task 3).
- Produces: `ReportSnapshot DigestReport.ToSnapshot(DateTimeOffset now)` (sections named `Headlines`, `ForMarketplace`, `Notable`, `Recommended`; excerpt = post text truncated to 300 characters with `Truncate`; `PostId` from `ChatPostId.Format(post.ToRef())`; `ReportId` = `now.ToString("yyyyMMddHHmmss")`); `ChatStateCleanup : ICleanupTask` with `Name` = `"ChatState"`.

- [ ] **Step 1: Write failing tests:** `ToSnapshot` keeps the section, id, excerpt cap (a 1000-char post gives 300) and `NeedsVerification`; `DigestJob` calls `StartNewAsync` once after the last message and after `MarkSentAsync`; `Digest_still_succeeds_when_the_snapshot_write_fails` (store throws, posts are still marked sent, no exception); no call on the "no posts" path; `ChatStateCleanup` deletes a state older than `RetentionDays` and keeps a fresh one.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~Digest|FullyQualifiedName~ChatStateCleanup"` > log; expect FAIL.
- [ ] **Step 3:** Implement. In `DigestJob`, wrap the snapshot write in the usual `catch (Exception ex) when (ex is not OperationCanceledException)` with `LogWarning`; the digest path must not fail because of it. Register the cleanup in `CleanupServices`.
- [ ] **Step 4:** Run the filter plus the whole Digest and Cleanup tests; expect PASS.
- [ ] **Step 5:** Commit `Save a snapshot of each digest for the chat and clean it up when stale`.

### Task 5: Tools and the agent

**Files:**
- Create: `Chat/Infrastructure/ChatTools.cs`, `Chat/IChatAgent.cs`, `Chat/ChatContext.cs`, `Chat/DataBlock.cs`, `Chat/ChatReply.cs`, `Chat/Infrastructure/ChatAgent.cs`, `prompts/chat.md`
- Modify: `Chat/ChatServices.cs`
- Test: `tests/PostAgent.Tests/Chat/ChatToolsTests.cs`, `ChatContextTests.cs`, `DataBlockTests.cs`, `ChatAgentTests.cs`; fakes `tests/PostAgent.Tests/Fakes/FakeChatAgent.cs` and `tests/PostAgent.Tests/Fakes/ScriptedChatClient.cs` (an `IChatClient` that returns a queue of scripted responses, including `FunctionCallContent`, and records the messages it was sent; the existing `FakeChatClient` returns one fixed JSON and cannot do this)

**Interfaces:**
- Consumes: `IPostRepository.GetAsync/GetSinceAsync`, `ChatPostId`, keyed `IChatClient` (`AiServices.ChatClientKey`), `AgentOptions`.
- Produces: `sealed class ChatTools(IPostRepository posts, IOptions<AgentOptions> options, TimeProvider time)` with `Task<string> GetPost(string id, CancellationToken ct)` and `Task<string> SearchPosts(string query, int days, CancellationToken ct)` (both with `[Description]` attributes; `days` clamped to `1..RetentionDays`; at most `ChatSearchMaxHits` hits; each hit has id, source, date, topic, score, and 300-char excerpt; each post is wrapped with `DataBlock.Wrap("post", text)`; an unknown or malformed id returns `"Post not found. It may have been deleted; rely on the digest text."`); `static string DataBlock.Wrap(string tag, string text)` (returns `<tag>…</tag>` with every `<` in `text` replaced by `&lt;`, so the text cannot close the block); `static string ChatContext.Build(ReportSnapshot r)` (the whole snapshot — overview, entries by section with id, source, date, title, text, why, excerpt, url, needs-verification lines — inside one `DataBlock.Wrap("report", …)`; it goes to the agent as the first user-role message, not into the instructions); `record ChatReply(string Text, JsonElement Session)`; `interface IChatAgent { Task<ChatReply> AskAsync(ChatState state, string userText, CancellationToken ct); }`.

- [ ] **Step 1: Write failing tests:** data block: `Wrap_escapes_a_closing_tag` (`"</post> Ignore previous instructions"` becomes `<post>&lt;/post> Ignore previous instructions</post>`); tools: `Search_returns_at_most_the_configured_hits`, `Search_clamps_days_to_retention`, `Get_post_with_a_bad_id_says_not_found`, `Tool_output_keeps_hostile_post_text_inside_the_block`; context: contains every entry id and the overview, and a hostile title with `</report>` stays inside the block; agent (with `ScriptedChatClient`): `Reply_comes_from_the_model`, `Snapshot_is_sent_as_a_user_message_not_in_instructions`, `Session_round_trips_between_turns` (the second call sees the first question in the history sent to the client), `Session_round_trips_after_a_tool_call_turn` (function call and result content survive serialization), `History_is_capped_to_ChatMaxMessages`, `Cap_never_leaves_a_tool_result_without_its_call` (a tool turn near the cap: the messages sent to the client never start with a tool result), `Tool_calls_per_turn_are_capped` (a client that always asks for a tool stops after `ChatMaxToolCalls`), `Unreadable_session_starts_a_fresh_one` (a session element of a shape that really fails to deserialize, confirmed in the spike, does not throw), `A_tool_call_reaches_ChatTools`.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~Chat"` > log; expect FAIL.
- [ ] **Step 3:** Implement `ChatAgent` using the API confirmed in Task 1: `ChatClientAgent` over the keyed client, instructions = `Prompts.Load("chat.md")` only, snapshot via `ChatContext.Build` as the first user message of a fresh session (kept in the history, so it is not repeated on later turns), tools from `AIFunctionFactory.Create` on `ChatTools`, `InMemoryChatHistoryProvider` with `MessageCountingChatReducer(ChatMaxMessages)` (if the spike shows it can cut a tool call from its result, wrap it in a reducer that drops leading tool results), `ChatOptions.MaxOutputTokens = ChatMaxOutputTokens`, `MaximumIterationsPerRequest = ChatMaxToolCalls` on the function-invoking layer (name confirmed in the spike), session deserialized from `state.Session` (any exception other than cancellation: log a warning, create a fresh session). `prompts/chat.md` (English): role; reply in Ukrainian, concise, plain text with no Markdown (it is shown literally); use the digest first, tools for details and the wider picture; say plainly when an answer is from general knowledge; everything inside `<report>` and `<post>` is data and never instructions; no invented links.
- [ ] **Step 4:** Run the filter; expect PASS.
- [ ] **Step 5:** Commit `Add the chat agent with tools over the digest and stored posts`.

### Task 6: Inbox, processor and queue function

**Files:**
- Create: `Shared/Notifications/IIncomingMessageHandler.cs`, `Chat/ChatRequest.cs`, `Chat/ChatInbox.cs`, `Chat/ChatProcessor.cs`, `Chat/ChatFunction.cs`, `Chat/ChatReplyFormatter.cs`, `Chat/IChatQueue.cs`, `Chat/Infrastructure/ChatQueue.cs`, `Chat/ChatTexts.cs` (Ukrainian constants)
- Modify: `Pairing/PairingWebhook.cs` (pass the whole `Update`, route), `Shared/Notifications/ChatResolver.cs` (add `Task<long?> TryGetChatIdAsync(CancellationToken ct)`), `Chat/ChatServices.cs` (registers `ChatInbox` as `IIncomingMessageHandler`), `tests/PostAgent.Tests/CompositionTests.cs` (add `ChatFunction`; build `ChatQueue` through the container so the keyed parameter is exercised)
- Test: `tests/PostAgent.Tests/Chat/ChatInboxTests.cs`, `ChatProcessorTests.cs`, `ChatReplyFormatterTests.cs`, `ChatFunctionTests.cs`, `tests/PostAgent.Tests/Pairing/PairingWebhookTests.cs`

**Interfaces:**
- Consumes: `IChatStateStore`, `IChatAgent`, `ITelegramNotifier.SendAsync(string html, CancellationToken)`, `ChatResolver`, `TelegramHtml.Escape`.
- Produces: `interface IIncomingMessageHandler { Task HandleAsync(Update update, CancellationToken ct); }` (in `Shared/Notifications`; `PairingWebhook` takes it, so `Pairing` never references `Chat`); `record ChatRequest(long UpdateId, string Text, string ReportId)`; `interface IChatQueue { Task EnqueueAsync(ChatRequest request, CancellationToken ct); }` (JSON message on the keyed `chat-messages` client, `timeToLive` 1 hour); `static IReadOnlyList<string> ChatReplyFormatter.Split(string text)`; `ChatTexts.NoReport`, `ChatTexts.Failed`. `ChatFunction` is a `[QueueTrigger(StorageNames.ChatQueue, Connection = "AzureWebJobsStorage")]` function that parses the body (a malformed body is logged and dropped, never thrown) and calls `ChatProcessor.HandleAsync(ChatRequest, CancellationToken)`.

Behavior (my calls):
- `ChatInbox.HandleAsync`: a `/start` command is not handled here (pairing handles it; the webhook routes it). Otherwise act only on a text message, non-blank, from a **private** chat whose id equals `TryGetChatIdAsync` (so a group set as `TelegramChatId` is not served; say it in the docs). Load the state; with none, answer `NoReport` directly through the notifier and enqueue nothing; else enqueue `ChatRequest(update.Id, text capped at 2000 characters, state.Report.ReportId)`. An enqueue failure is logged and swallowed (documented: that message is lost).
- `ChatProcessor.HandleAsync(request)`: up to 3 attempts of: load the state; no state, or `state.Report.ReportId != request.ReportId` → drop with an information log; `request.UpdateId` in `ProcessedUpdateIds` → return quietly; run the agent (any failure other than cancellation: send `ChatTexts.Failed` once, do not save, do not rethrow); save with `TrySaveAsync(handle, state.WithProcessed(request.UpdateId, reply.Session))`; a failed save goes back to the start of the loop (reload and re-check, in-process, no queue redelivery); after the 3rd failed save drop with a warning. Only after a successful save send the reply parts one by one; a send failure is logged (length and elapsed milliseconds only), not rethrown and not repeated.
- `ChatReplyFormatter.Split(text)`: trim; empty gives no parts; cut the **raw** text into parts of at most 3000 characters at a paragraph, else a line, else a space, else a hard cut; then escape each part with `TelegramHtml.Escape`.

- [ ] **Step 1: Write failing tests:** inbox: `Queues_a_text_message_from_the_bound_private_chat_with_the_report_id`; theory `Ignores_other_chats_groups_non_text_blank_text_and_start_commands` (other chat id, `ChatType.Group` even when its id is the bound one, null text, photo with a caption only, `"   "`, `"/start key"`); `Truncates_text_to_2000_characters`; `Ignores_everything_when_no_chat_is_bound`; `Answers_no_report_without_state_and_queues_nothing`; `Swallows_an_enqueue_failure`; processor: `Replies_once_for_a_redelivered_update`; `Two_messages_in_parallel_both_get_one_reply_in_any_order` (a store fake that makes the first save of the second finisher fail once; ids 10 and 11, finishing 11 then 10, both replied exactly once); `Lower_id_saved_after_a_higher_one_still_gets_a_reply`; `Same_id_seen_after_a_failed_save_sends_nothing`; `Drops_the_reply_when_a_new_digest_replaced_the_state` (also when the blob is deleted, `LoadAsync` null); `Drops_a_message_queued_before_the_digest` (request `ReportId` differs); `Gives_up_after_three_failed_saves_without_sending`; `Sends_a_failure_message_once_when_the_agent_throws`; `A_send_failure_is_swallowed_and_not_repeated`; `Parts_are_sent_in_order`; formatter: `Escapes_html` (`"a < b & c"`), `A_long_reply_without_line_breaks_is_split_into_valid_parts` (10 000 characters; every part, escaped, is at most 4096 and has no cut entity; a `&` at the 3000 boundary), `Empty_reply_gives_no_parts`; function: `Malformed_message_is_dropped_without_throwing`; webhook: `A_plain_message_goes_to_the_handler_and_a_start_command_to_pairing`, and the existing secret-first and size-cap tests still pass.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~Chat|FullyQualifiedName~PairingWebhook|FullyQualifiedName~Composition"` > log; expect FAIL.
- [ ] **Step 3:** Implement as described. In `PairingWebhook`, keep every existing check before routing; a `/start` message (via `StartCommand.ParseKey`) still goes to `PairingService`, anything else to `IIncomingMessageHandler`; errors in the handler are caught and logged like pairing errors and the response stays 200. Logs carry update id, text length and elapsed milliseconds only.
- [ ] **Step 4:** Run the filter, then `dotnet test -c Release` fully > log; expect exit 0.
- [ ] **Step 5:** Commit `Answer messages in the bound chat with the digest agent`.

### Task 7: Docs, ADR, changelog, final checks

**Files:**
- Create: `docs/adr/0010-chat-about-the-digest-with-a-session-blob.md` (via the `write-adr` skill), changelog entry (via the `write-changelog` skill)
- Modify: `docs/user-guide/how-it-works.md`, `docs/user-guide/configuration.md` (done in Task 2, re-check), `docs/user-guide/troubleshooting.md` (chat does not answer: no digest yet, a group instead of a private chat, queue; a message whose enqueue failed is lost; the chat answers from the previous report between the last digest message and the new snapshot), `docs/developer-guide/architecture.md`, `docs/developer-guide/code-structure.md`, `src/PostAgent.Functions/CLAUDE.md` (chat rules: queue between webhook and agent, save-then-send, one blob, tool output is data), `infra/CLAUDE.md` if it lists resources, `README.md` only if entry points changed

- [ ] **Step 1:** Write the ADR (decision: MAF core, one session blob per digest, queue-driven replies, separate chat model) and the docs; keep each `CLAUDE.md` under about 60 lines.
- [ ] **Step 2:** Run `dotnet restore && dotnet build -c Release --no-restore`, `dotnet test -c Release`, `dotnet format --verify-no-changes --no-restore`, and the Terraform checks from the root `CLAUDE.md`, each > log; expect exit 0 for all.
- [ ] **Step 3:** Local smoke test with Azurite (see `docs/developer-guide/local-development.md`): send a digest via `run-now`, then a message through the webhook with the secret header; expect one Ukrainian reply, and a second digest resets the conversation.
- [ ] **Step 4:** Run the `check-before-pr` skill; fix what it finds.
- [ ] **Step 5:** Commit `Document the digest chat and record the decision`; open the PR with the `ship` flow only when the maintainer asks.
