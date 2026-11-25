# Sign in to Telegram from the bot through a Mini App

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

The collector reads channels as a Telegram user over MTProto. Its session is a blob (`tg-session/wtelegram.session`) uploaded by `src/TgLogin`,
a console tool that needs the .NET SDK, `az login`, a role on the storage account and the account's phone, code and 2FA password typed in a terminal.
When the session is revoked, the owner gets «Збір не вдався» every hour and has to repeat all of that from a computer.
The owner already talks to the bot from a bound private chat (ADR-0008, ADR-0014), and the bot already has a public webhook in the Function App.

Two constraints shape the options. Telegram blocks a sign-in when its login code is sent in a Telegram message, so the code cannot be typed into the bot chat.
And an MTProto sign-in is a dialogue (phone, code, password) that must happen on one connection, while a webhook call must answer in seconds.
Azure Logic Apps has only a Bot API connector, so it cannot sign in as a user and would still need our own code.

## Decision

We sign in from the bot. `/login` in a bound private chat queues a message (`tg-login`); `LoginFunction` takes the session lock, opens a fresh WTelegram client,
asks Telegram to send the code and sends a button that opens a Telegram Mini App (a page the Function App serves at `/api/telegram/login`).
The owner types the code, and the 2FA password if the account has one, into that page. The page posts them over HTTPS with its signed `initData`, which the function checks
(HMAC with the bot token, fresh `auth_date`, the user is the chat that started the attempt, a random attempt id) and puts into a short-lived attempt blob.
The waiting function reads them, finishes the sign-in, overwrites the session blob and reports in the chat. `TgLogin` stays as the fallback for the first installation.

## Options considered

- **Keep only `TgLogin`**: nothing new to secure, but every expiry needs a computer with the SDK and Azure access.
- **Azure Logic App**: no MTProto support, so the sign-in would still be our code, plus a new resource and a second place to secure.
- **Code typed into the bot chat**: the simplest, but Telegram may block a code sent in a message; typing it with spaces works around it only for now.
- **QR code (`auth.exportLoginToken`)**: no code to type, but the QR has to be scanned from a second device that holds the account; it cannot be scanned on the same phone.
- **2FA password in Key Vault**: one input less, but the password of the reader account would sit at rest and be readable by the function identity at all times.
- **Mini App with code and password (chosen)**: works on one phone, the code never travels as a Telegram message, the password exists only for the minutes of a sign-in.

## Consequences

Re-signing takes a minute from the phone. The cost is a second anonymous endpoint and a stored secret in transit: the attempt blob holds the code and the password
until the waiting function reads them (it deletes them at once) and is gone when the attempt ends (5 minutes at most). The endpoint trusts only `initData` signed with the bot token,
so a leaked bot token now also allows submitting a code; it still needs the code that Telegram sends to the reader account.
The sign-in holds the session lock, so a collection that starts meanwhile is skipped. Only one attempt runs at a time. A sign-in that Telegram routes to email or sign-up
is not handled and points the owner to `TgLogin`. Revisit if the bot gets users who are not trusted with the reader account: today every bound chat may start a sign-in.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
