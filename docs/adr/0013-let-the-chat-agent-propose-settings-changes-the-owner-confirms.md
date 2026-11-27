# Let the chat agent propose settings changes that the owner confirms

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

ADR-0012 put the settings in the `/settings` menu. The owner also talks to the agent in the same chat (ADR-0010), and wants to say "remove a source" or "make alerts rarer" there and have the agent
ask what is unclear ("which one?", with the list) and then change it. But the model reads channel posts, so a post can carry text that tries to make it change the settings,
and the model's own words can claim a change that is not the one that would be made.

## Decision

The chat agent gets tools that only build a plan: `get_settings` (read) and `propose_*` (set, reset, reset group, add, remove source, toggle alert source). A tool takes structured arguments,
checks the plan with a dry run of `SettingsEditor` (the same rules and validator as the menu) and adds a step to the plan of that turn; a rejected step is explained to the model, which can correct it or ask the owner.
Nothing is stored by the model. After the answer is saved, code stores the plan as a proposal in `settings/overrides.json` and sends a separate message built from the structured steps
(`SettingsProposalMessage`: label, old value, new value, escaped) with the buttons "Застосувати" and "Скасувати". Only a tap from the bound private chat applies it, as one checked write, and the proposal is then removed.
A proposal lives 30 minutes; at most five are kept. The prompt tells the model to ask when the target is not clear and to say that the plan needs the tap.

## Options considered

- **Apply at once when the owner asked in chat**: simplest, but a post that persuades the model changes the settings with no human step.
- **The model writes the confirmation text**: the text can differ from what is applied; building it from the steps removes that gap.
- **Keep the plan only in the conversation**: the button press arrives as a separate update without the conversation; the blob already holds the menu's state, so the proposal goes there.

## Consequences

- No new infrastructure and no new setting. The overrides blob gets an optional `Proposals` list; old blobs read as before.
- A change costs one tap. The cost of the extra tools is a few hundred tokens in every chat request.
- Secrets, schedules and prompts stay out of reach: tools only accept keys from `SettingsCatalog`.
- Revisit if the agent has to serve several users, or if the owner wants some changes applied without a tap.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->

2026-11-25: partly changed by [ADR-0014](0014-serve-several-chats-with-shared-settings.md): several chats can be bound, with one conversation per chat and shared settings.
2026-11-26: partly changed by [ADR-0021](0021-let-the-chat-agent-do-non-invasive-actions-at-once.md): non-invasive actions (rating a post, following a topic, clearing the context) are done at once; settings, sources, prompts and the profile still need the tap.
2026-11-27: the reader profile is gone ([ADR-0022](0022-keep-a-reader-memory-the-chat-agent-manages-itself.md)): the agent keeps a reader memory at once; settings, sources and prompts still need the tap.
