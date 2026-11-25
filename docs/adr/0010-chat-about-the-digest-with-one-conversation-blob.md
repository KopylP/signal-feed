# Chat about the digest with one conversation blob

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

The reader wants to reply to the digest in Telegram and ask more about it. The webhook from ADR-0008 already receives the bot's messages, but understands only `/start`.
The digest itself is not stored anywhere: only the "sent" marks on posts. The answer needs the digest and, for the wider picture, the stored posts.
A new digest must start a fresh conversation, and old conversations must not accumulate. The webhook has to answer Telegram quickly or Telegram resends the update, while a model call with tools takes seconds.
Microsoft Agent Framework reached 1.0 in April 2026; its Azure Functions durable hosting is still preview and brings Durable Task with its own storage and endpoints.

## Decision

We answer with a Microsoft Agent Framework agent (core package `Microsoft.Agents.AI`, no durable hosting) over a separate, stronger model (`OpenAIChatModel`, default `gpt-6.1-terra`).
The webhook checks the message and puts it on a queue; a queue function asks the model, saves the conversation and then sends the answer. The conversation is a single blob
(`chat-state/current.json`) holding a snapshot of the digest, the ids of the last 20 handled updates and the serialized agent session. Every digest overwrites the blob, and cleanup deletes it when stale.
The agent has two tools, `get_post` and `search_posts`; everything channels wrote reaches the model only inside escaped data blocks.

## Options considered

- **Durable agents hosting for Azure Functions**: gives state and endpoints for free, but is preview, adds Durable Task storage and its own HTTP surface, and fits several sessions, not one chat.
- **Answer inside the webhook**: simplest, but a slow model call makes Telegram resend the update and the answer is duplicated.
- **Table row per message, or a conversation per digest kept for history**: queryable, but grows without bound and nobody asked for history; one overwritten blob is the bound.
- **A high watermark of `update_id` for dedupe**: smaller state, but parallel messages can finish in reverse order and lose one, and Telegram may restart ids; a set of the last 20 ids has neither problem.
- **Digest only in the context, no tools**: cheaper per turn, but no wider picture; chosen instead is the digest with short excerpts plus tools for full text and search.

## Consequences

A question costs one model call when the digest answers it (about 2 cents at the default model) and up to `ChatMaxToolCalls + 1` calls when the agent uses tools, each resending the digest and the conversation; there is no daily limit; only the bound private chat reaches it. A question asked just before a new digest is dropped on purpose.
Between the last digest message and the snapshot write (a few seconds) the chat answers from the previous digest. A failed snapshot write is logged and does not fail the digest.
A question whose enqueue failed is lost, and a send failure is never repeated, because a repeat would duplicate a delivered message. The chat works only in a private chat.
Revisit if the agent has to serve several chats or keep history, or when durable agents hosting becomes stable.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->

2026-11-25: partly changed by [ADR-0014](0014-serve-several-chats-with-shared-settings.md): several chats can be bound, with one conversation per chat and shared settings.
