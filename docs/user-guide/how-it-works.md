# How it works

**Audience:** operators who want to understand why the agent behaves the way it does, before tuning it.
Setting names (in `code`) are explained in [Configuration](configuration.md). The technical view is in the [architecture](../developer-guide/architecture.md).

## The life of a post

![The life of a post: Telegram, collect, score with AI, then an instant alert or the daily digest to you in Telegram](images/life-of-a-post.drawio.svg)

1. **Collect.** Every hour the agent reads new messages from your sources. The first time it sees a source, it reads the last 24 hours.
2. **Score.** Each post gets a score, a topic and a short reason from the AI model, judged against your rules in [`prompts/scoring.md`](../../prompts/scoring.md). Near-duplicates are recognized.
3. **Deliver.** Once a day the best posts become the digest. Alerts for groups go out sooner, within the hour.

A daily cleanup removes old posts, so storage stays small.

## Channels and groups

**Channels.** Every post is scored on its own.

**Groups** are noisy, so they are handled differently:

- If only a few messages arrived since the last run (up to `MaxItemsPerGroup`, 5 by default), each is a separate post.
- If there are more, messages are split by time into at most `MaxItemsPerGroup` bundles of similar size, and each bundle is scored with one AI call.
  The busier the group, the bigger the bundle, but the cost does not grow.
- Inside a bundle the messages with the most reactions, forwards and replies come first, so the key ones are not lost.
- The minimum length `MinPostChars` applies to single messages only. Short replies stay in a bundle, because their number shows a burst.

## Alerts

- Only for groups listed in `AlertSources`. Channels never trigger alerts.
- An alert is sent when a message or bundle scores at least `AlertThreshold` (9), or a bundle has at least `BurstMinPosts` (8) messages.
- One group triggers at most one alert per `AlertCooldownHours` (3), and one alert holds at most `AlertMaxPerRun` (3) entries.
- During `AlertQuietHours` (for example `23:00-07:00`) nothing is sent, and those posts go into the digest instead.
- A post that already triggered an alert is not repeated in the digest.

## The digest

After the best posts are chosen (`DigestSize`, 7 by default), one more AI call writes the digest, using [`prompts/digest.md`](../../prompts/digest.md).
It arrives in sections (a section with nothing in it is left out):

- **🔥 Головне за день**: the most important news with the point and why it matters.
- **💼 Корисно для Collaborator**: what can affect the catalog, demand or the marketplace.
- **👀 Варто звернути увагу**: less obvious cases, tools, studies and opinions.
- **📖 Рекомендую прочитати**: up to three posts worth reading in full.
- **⚠️ Потребує перевірки**: claims to check before relying on them.
- **📌 Можна пропустити**: how many posts were left out (near-duplicates and less important ones).

The model never writes links, channels or dates: the agent adds them from the post data, so they cannot be invented. A long digest is split into several Telegram messages,
and a section that continues in the next message repeats its header.

If OpenAI is unavailable or its answer is invalid (including an answer that names none of the selected posts), the digest still arrives, as a plain list of the posts with the short scoring reasons.
The digest is always sent, even when nothing stood out.

Posts that are still unscored two hours after they were published (scoring failed, see [troubleshooting](troubleshooting.md)) cannot be in the digest.
The digest then adds a separate message «⚠️ Не оцінено N постів», and sends it instead of «За добу нових постів не було» when nothing was scored, so a broken OpenAI key does not look like a quiet day.

## Asking about the digest

After a digest arrives, reply to the bot in the same private chat to ask more: «розкажи детальніше про пункт 3», «що з цього важливе для каталогу?».
An AI assistant answers in Ukrainian, plain text, using the digest you received. If the digest is not enough, it can open the full text of a post and
search the posts stored for the last `RetentionDays` days (for example «що ще писали про це вчора?»). A question the posts do not cover is answered from the model's own knowledge,
and the answer says so. It does not browse the web.

- **One conversation per digest.** A new digest starts a new conversation and the old one is forgotten, so answers never mix two digests. Alerts do not reset it.
- **Private chat only.** The bot answers in the private chat with it. If `TelegramChatId` points to a group, the digest still goes there, but questions in the group are not answered.
- **Bounds.** A conversation keeps the last `ChatMaxMessages` messages; a question is cut at 2000 characters; a question that waited in the queue for more than an hour is dropped.
- **Cost.** A question the digest answers is one model call, about 2 to 4 cents with the default `OpenAIChatModel` and `medium` reasoning. When the assistant uses tools it makes one more call per round, up to `ChatMaxToolCalls + 1`, and every call resends the digest and the conversation, so a tool-heavy question can cost several times more (see [configuration](configuration.md#settings)).
- Before the first digest the bot answers «Ще немає дайджесту, про який можна поговорити. Напишіть після наступного дайджесту.»

## Data retention

Posts are kept for `RetentionDays` (1 by default: yesterday and today) and then deleted by day. The read position of each source is never deleted.
The digest can look back 48 hours only within the data that remains, so with the default it covers yesterday and today.
The conversation about the latest digest is deleted by the same cleanup once its digest is older than `RetentionDays`.

## Next

[Install it](installation.md), or see [Configuration](configuration.md) to change thresholds and schedules.
