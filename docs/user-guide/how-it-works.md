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

Write to the bot in the private chat to ask more (after a digest, or before the first one): «розкажи детальніше про пункт 3», «що з цього важливе для каталогу?». The agent can also show and change parts of its own prompts and keep a short profile of you, each change only after you confirm the exact before and after ([configuration](configuration.md#prompts-from-the-chat)). You can also use Telegram's reply or quote on any message: the quoted part (or the message you replied to) goes to the model with your question, so «розкрий цю тему» knows which topic you mean.
An AI assistant answers in Ukrainian, plain text, using the digest you received. If the digest is not enough, it can open the full text of a post and
search the posts stored for the last `RetentionDays` days (for example «що ще писали про це вчора?»). A question the posts do not cover is answered from the model's own knowledge,
and the answer says so. It does not browse the web.

- **Settings.** You can also ask the agent to change settings and sources; it only prepares a plan, and you confirm it with a button ([configuration](configuration.md#by-asking-the-agent)).
- **One conversation per digest.** A new digest starts a new conversation and the old one is forgotten, so answers never mix two digests. Alerts do not reset it.
- **Live answer.** While the assistant writes, the chat first shows «💭 Думаю», then the answer as a preview that grows from its third word (`ChatStreamEnabled`); the finished message then replaces it. The confirmation buttons are colored: green «Застосувати», red «Скасувати».
- **Several chats.** Every paired chat gets the digest and the alerts and can ask questions or open `/settings`. Each chat has its own conversation, so questions and answers are not shared, but settings and sources are: a change made in one chat applies to all, and a plan the agent prepares can be confirmed only in the chat that got it.
- **Private chat only.** The bot answers in the private chat with it. If `TelegramChatId` points to a group, the digest still goes there, but questions in the group are not answered.
- **Bounds.** A conversation keeps the last `ChatMaxMessages` messages; a question is cut at 2000 characters; a question that waited in the queue for more than an hour is dropped.
- **Cost.** A question the digest answers is one model call, about 2 to 4 cents with the default `OpenAIChatModel` and `medium` reasoning. When the assistant uses tools it makes one more call per round, up to `ChatMaxToolCalls + 1`, and every call resends the digest and the conversation, so a tool-heavy question can cost several times more (see [configuration](configuration.md#settings)).
- **Before the first digest** the chat works too: the agent has no digest to rely on, so it answers from the stored posts and from its own knowledge and says which. When a digest arrives, it becomes the context of the conversation and replaces what was there.
- **Clearing the context.** Send `/clear` to forget the earlier questions and answers (the digest stays), or ask the agent («забудь розмову»): it then prepares the action and you confirm it with a button, like a settings change.

## Renewing the Telegram session

The agent reads channels as a Telegram user (the reader account). When Telegram ends that session, every collection fails with «Збір не вдався» and a hint to send `/login`.
Renew it from the bot, in your paired private chat ([ADR-0016](../adr/0016-sign-in-to-telegram-from-the-bot-through-a-mini-app.md)):

1. Send `/login`. The bot answers «Готую вхід…», then sends «📱 Ввести номер».
2. The button opens a form inside Telegram. Type the phone number of the reader account (`+380…`).
3. Telegram sends the login code to that account (the «Telegram» chat in its app), and the bot sends «🔑 Ввести код».
4. Type the code. If the account has a two-step verification password, the bot sends «🔒 Ввести пароль»: type it there.
   The form checks the password on your phone and sends only a one-time proof: the password itself never leaves the phone and is not stored anywhere.
5. Every paired chat gets «✅ Сесію акаунта-читача оновлено» with the account's name and masked number.

Type the code only into the form, never into a chat: Telegram blocks a sign-in whose code was sent as a message.
A sign-in lasts at most 5 minutes and allows 3 wrong answers; only one runs at a time, and a collection that starts meanwhile is skipped.
The form is accepted only from the paired chat that sent `/login`. Any paired chat may sign in any account, which is why every chat is told who reads the channels now:
if you did not expect the message, send `/login` and sign in with your own account.

## Data retention

Posts are kept for `RetentionDays` (1 by default: yesterday and today) and then deleted by day. The read position of each source is never deleted.
The digest can look back 48 hours only within the data that remains, so with the default it covers yesterday and today.
The conversation about the latest digest is deleted by the same cleanup once its digest is older than `RetentionDays`.

## Next

[Install it](installation.md), or see [Configuration](configuration.md) to change thresholds and schedules.
