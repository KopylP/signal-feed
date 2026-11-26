# Let the chat agent do non-invasive actions at once

- Status: Accepted
- Date: 2026-11-26
- Deciders: Maintainer

## Context

[ADR-0013](0013-let-the-chat-agent-propose-settings-changes-the-owner-confirms.md) made every change the chat agent makes a proposal that the owner confirms with a button, because a post the model reads could try to make it change the settings.
Since then the agent also follows topics and clears the conversation through proposals, and the reader rated digest entries with 👍/👎 buttons under every digest (a separate message after it).
On 2026-11-26 the owner found the rating buttons hard to read and asked to rate a news item by telling the agent instead («+», «лайк», «цікаво», usually as a reply to the item),
and to stop asking for a tap where an action cannot hurt: a tap for «стеж за темою» or «забудь розмову» only slows the reader down.

## Decision

We split the agent's actions into two kinds. **Non-invasive** actions only record the reader's taste, add or stop alerts the reader asked for, or forget the conversation, are bounded and undone by another such action:
rating a post (`rate_post`), following and unfollowing a topic (`add_watch`, `remove_watch`) and clearing the context (`clear_context`). Their tools do them at once, with the same checks as before
(`SettingsEditor` for topics, `IFeedbackStore` for votes); clearing is applied by `ChatProcessor` when it saves the answer, because the running turn still writes the conversation.
**Invasive** actions change how the agent works for every chat or what it knows about the reader (settings, sources, alert sources, prompts, the profile) and stay proposals confirmed by the button, as in ADR-0013.
The rating buttons under the digest are removed: the reader rates through the agent.

## Options considered

- **Keep every action behind the button**: safest against a persuasive post, but a tap for a like defeats the purpose and was the reason the owner asked.
- **Direct actions for everything the owner asks for**: fewest taps, but a post that persuades the model could then change sources or prompts with no human step.
- **Keep the buttons and add rating by the agent**: two ways to do one thing, and the buttons were the part the owner found unreadable.

## Consequences

- A post that persuades the model can, at worst, add a vote (one point of hint in scoring, capped and replaced by the reader's next vote), follow a topic or forget the conversation. The prompt still says to act only on the reader's own message.
- A new agent action must be classed: a tool that changes shared settings, sources, prompts or the profile is a `propose_*` tool; only a bounded, reversible action that affects the reader's own alerts, votes or conversation may act at once.
- No new infrastructure and no new setting; `FeedbackEnabled` now switches the agent's rating and the scoring hint. Stored proposals with topic or clear steps still apply.
- The digest is one message fewer, and a rating costs a chat turn (one model call) instead of a tap.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
