# Bind the Telegram chat with a pairing key over a webhook

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

The digest and alerts go to one Telegram chat. Until now its numeric id was an App Setting filled in by hand (`telegram_chat_id`), so the operator had to find the id
before the first digest. The agent needs only this one chat, and its owner is the only person who should be able to claim it.
The bot is used only to send, so no webhook or `getUpdates` poller exists yet. The reader account works over MTProto and does not conflict with a bot webhook.

## Decision

We add an anonymous HTTP function that is the bot's Telegram webhook. The user sends `/start <pairing key>` to the bot in a private chat; if the key matches,
the function stores the chat id in the `ChatBinding` table and confirms in the chat. The notifier reads the id from that table.
Both the pairing key and the webhook secret token are Key Vault secrets (ADR-0003). The first successful pairing wins: later `/start` messages from other chats are ignored
until the operator deletes the row. `TelegramChatId`, if set, still overrides the table and turns pairing off.

## Options considered

- **Keep the manual chat id**: nothing new to secure, but the operator must look the id up through a third-party bot or `getUpdates`.
- **A timer that polls `getUpdates` while no chat is bound**: no public endpoint, but the pairing waits up to a minute, and it conflicts with a webhook if one is added later.
- **Webhook with a pairing key (chosen)**: instant, standard Telegram flow; costs one public endpoint and one `setWebhook` call during installation.
- **Store the id in an App Setting or Key Vault**: changing an App Setting restarts the app and needs management rights; the id is not a secret.

## Consequences

Installation no longer needs the chat id, but needs two new secrets and a one-time `setWebhook` call (see the installation guide). The endpoint is public, so it rejects any request
without the webhook secret token, compares keys in constant time and logs neither secret. A digest or alert sent before pairing fails and keeps its posts for the next run.
Resetting the chat means deleting the `ChatBinding` row. Revisit if the agent has to serve several chats: the table holds a single row.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->

2026-11-25: partly changed by [ADR-0014](0014-serve-several-chats-with-shared-settings.md): several chats can be bound, with one conversation per chat and shared settings.
