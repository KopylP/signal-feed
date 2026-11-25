# Override settings from the Telegram bot through a blob

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

Every parameter was an Azure App Setting, so adding a channel or raising a threshold meant `az` or Terraform, and the app restarted. The owner wants to change a useful part of them
from the bot chat, the place where they already read the digest. Jobs read `IOptions<AgentOptions>`, which is fixed when the app starts, and 17 classes use it.
The bot can only show buttons under a message (inline keyboard); typed text is needed just for a few values (a channel name, quiet hours).

## Decision

Values set in the bot are stored in one blob, `settings/overrides.json`, written with an ETag condition. `IEffectiveOptions` returns the App Setting `AgentOptions` with the overrides on top,
checked as a whole by the same validator that runs at startup, and caches the result for 30 seconds. A job takes one snapshot at the start of a run. If the combination is invalid, the App Settings win and the menu says so.
Precedence is bot, then App Setting, then the default in code. Only a catalog of parameters can be changed (`SettingsCatalog`); secrets, schedules, prompts, `RetentionDays` and infrastructure values never can.

The menu is `/settings`: one message edited in place, with inline buttons. Navigation is stateless (the screen is in `callback_data`, and a number button carries the target value, so a double tap is harmless).
The only stored state is one pending question ("send the value"), kept in the same blob and expiring after 10 minutes. Models and reasoning effort are chosen from a fixed list of buttons, never typed.
The model is set on every request (`ModelSelectingChatClient`), because the chat clients are singletons.
A source added from the bot is saved as not verified; the first collection that reads it without an error confirms it, and a failure offers a "remove" button. The webhook is registered with `callback_query` as well as `message`.

## Options considered

- **A custom configuration provider with `IOptionsMonitor`**: consumers barely change, but reloading is hidden, validation bypasses `AgentOptionsValidator`, and a bad blob could stop startup.
- **Table Storage, one row per setting**: fits numbers, fits badly the pending question and the source list with flags.
- **One stored state machine for the whole menu**: needs a write on every tap and breaks when old menu messages are pressed; encoding the screen in the button is simpler and idempotent.
- **Checking a typed model name with OpenAI**: needs a call inside the webhook; a fixed list of buttons removes the problem. Adding a model is a code change.
- **Verifying a channel before adding it**: needs the Telegram user session, which a collection holds; verifying lazily on the first collection avoids a second consumer of the session.

## Consequences

- A new container `settings` (Terraform, no standing cost) must exist before the code runs; without it the bot cannot save, and jobs keep using the App Settings.
- `setWebhook` must be called again with `allowed_updates=["message","callback_query"]`; until then `/settings` opens but its buttons do nothing.
- A change reaches a running job within about 30 seconds; another instance can use the old value for that long.
- Once the source list is changed in the bot, a later change of the `Sources` App Setting is ignored until the group is reset in the menu.
- The collector deletes the cursors of sources that are not in the effective list. If the overrides blob is unreadable and the app falls back to the App Settings, the cursors of sources added in the bot are dropped; adding them again replays at most `FirstRunLookbackHours` and stored posts are not duplicated.
- Revisit if the agent has to serve several users, or if the owner wants the schedules and prompts in the bot too.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
