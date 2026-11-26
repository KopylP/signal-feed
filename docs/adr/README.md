# Architecture decision records

An ADR captures one significant decision: the context, what we chose, the alternatives and the consequences.
The code shows what we built; an ADR explains why, so the reasoning survives after the chat or the memory of it is gone.

## When to write one

Write an ADR when the decision is hard to reverse or shapes other work: hosting and runtime, storage model, identity and access,
how infrastructure is deployed, the shape of the pipeline, the testing approach, the contract between prompts and code.
Skip it for small, local or easily reversible choices, temporary workarounds, and anything already covered by a standard.
If a future maintainer would ask "why on earth is it done like this?", write it down.

## Rules

- One decision per file, named `NNNN-imperative-title.md` (zero-padded sequence number, lowercase, dashes).
- Start from [`template.md`](template.md). Status is one of `Proposed`, `Accepted`, `Deprecated`, `Superseded by ADR-NNNN`.
- Do not change the decision of an accepted ADR. To change course, write a new ADR and mark the old one `Superseded by ADR-NNNN`
  (the only edit allowed to the old file besides dated notes in `Updates`).
- Keep each ADR short, in English, and link related ADRs and code paths instead of copying them.
- Add every new ADR to the index below in the same commit. The `write-adr` skill (`.claude/skills/write-adr`) walks through this.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-host-on-flex-consumption.md) | Host on Azure Functions Flex Consumption | Accepted |
| [0003](0003-keep-secret-values-out-of-terraform-state.md) | Keep secret values out of Terraform state | Accepted |
| [0004](0004-run-terraform-from-a-manual-workflow.md) | Run Terraform from a manual workflow | Accepted |
| [0005](0005-release-every-deploy-with-a-build-counter-version.md) | Release every deploy with a build-counter version | Accepted |
| [0006](0006-fix-formatting-and-validate-terraform-in-the-build-job.md) | Fix formatting and validate Terraform in the build job | Accepted |
| [0007](0007-name-each-release-with-a-changelog-word.md) | Name each release with a changelog word | Accepted |
| [0008](0008-bind-the-telegram-chat-with-a-pairing-key-over-a-webhook.md) | Bind the Telegram chat with a pairing key over a webhook | Accepted |
| [0009](0009-send-the-digest-as-sections-in-several-messages.md) | Send the digest as sections in several messages | Accepted |
| [0010](0010-chat-about-the-digest-with-one-conversation-blob.md) | Chat about the digest with one conversation blob | Accepted |
| [0011](0011-call-the-chat-model-through-the-responses-api.md) | Call the chat model through the Responses API without stored output | Accepted |
| [0012](0012-override-settings-from-the-telegram-bot-through-a-blob.md) | Override settings from the Telegram bot through a blob | Accepted |
| [0013](0013-let-the-chat-agent-propose-settings-changes-the-owner-confirms.md) | Let the chat agent propose settings changes that the owner confirms | Accepted |
| [0014](0014-serve-several-chats-with-shared-settings.md) | Serve several Telegram chats with shared settings | Accepted |
| [0015](0015-edit-prompt-sections-and-a-reader-profile-from-the-chat.md) | Edit prompt sections and keep a reader profile through the chat | Accepted |
| [0016](0016-sign-in-to-telegram-from-the-bot-through-a-mini-app.md) | Sign in to Telegram from the bot through a Mini App | Accepted |
| [0017](0017-encrypt-the-telegram-session-with-a-key-vault-key.md) | Encrypt the Telegram session with a Key Vault key | Accepted |
| [0018](0018-keep-daily-summaries-as-long-memory.md) | Keep a small summary per day as the long memory | Accepted |
