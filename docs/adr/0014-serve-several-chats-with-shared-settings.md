# Serve several Telegram chats with shared settings

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

ADR-0008 binds one chat (the first pairing wins, one `ChatBinding` row) and said to revisit if the agent had to serve several. It now has to: the owner wants a second chat (a test account, or a colleague)
that gets everything the first one gets. ADR-0010 keeps one conversation blob (`chat-state/current.json`), and ADR-0012 and ADR-0013 let the bound chat change the settings and confirm plans.
The sources, the settings and the posts belong to the agent, not to a chat, so they stay single.

## Decision

We bind up to `TelegramMaxChats` chats (5 by default). Each chat that sends `/start <pairing key>` is added to `ChatBinding` (one row per chat id; the old `digest` row still counts as a chat).
The digest, alerts and failure messages go to every bound chat; a failed send to one chat is logged and does not stop the others, and the job fails only when no chat received the message.
Conversations are isolated: one blob per chat (`chat-state/<chat id>.json`), a question and its answer carry the chat id, and the menu and the proposals answer the chat that asked.
Settings and sources stay shared: every chat writes to the same `settings/overrides.json` through `SettingsEditor`, so a change made in one chat is seen in all. A proposal is applied only from the chat that received it.
`TelegramChatId`, when set, still overrides the table and means exactly one chat.

## Options considered

- **A shared conversation for all chats**: one blob as before, but every chat would see and extend the others' questions, and the model's context mixes readers.
- **One copy of the agent (settings, sources, posts) per chat**: full isolation, but the costs multiply (reading and scoring run once per chat) and the owner must keep several configurations in sync.
- **Copies only, no chat for the second chat**: the least code, but the owner asked for the second chat to work the same way.
- **Per-chat conversation and menu, shared settings (chosen)**: one pipeline and one cost; the code touches the binding, the sender, the conversation store and the proposals.

## Consequences

- Any chat that holds the pairing key joins until the limit; the key is rotated when it should stop working. A chat is removed by deleting its `ChatBinding` row.
- The send is a POST that is never retried (ADR-0009 area), so one chat's timeout cannot cause a duplicate in another.
- A conversation started before this change (`current.json`) is not carried over; the next digest starts a new one in every chat.
- All chats are equally trusted: each can change the shared settings. Revisit if the chats belong to people who must not be able to change them (an owner role).

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
