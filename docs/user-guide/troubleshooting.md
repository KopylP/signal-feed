# Troubleshooting

**Audience:** operators. Find your symptom, read the cause, apply the fix. Known limitations are at the end.

## Symptoms

| Symptom | Likely cause | What to do |
|---|---|---|
| The app does not start; the log names a setting | Startup validation failed: a required value is missing or out of range | Fix the setting named in the log. See [Validation](configuration.md#validation) |
| Startup fails with an error naming a secret | A Key Vault reference is unresolved | Check that the function's identity has the `Key Vault Secrets User` role and that the secret exists. Then run `az functionapp restart` |
| «Збір не вдався» ("Collection failed") arrives every hour | Collection fails, most often because the Telegram session expired | Repeat the Telegram sign-in ([installation, step 4](installation.md#step-4-sign-in-to-telegram)). The message repeats hourly until the cause is fixed |
| One group or channel never appears | The reading account cannot see it | Join it with the reading account |
| A new busy group is skipped on the first run | Reading a day of history means many requests, and Telegram answered `FLOOD_WAIT` | Nothing: the source is skipped and retried on the next run |
| No digest or alert arrives, the log says «No Telegram chat is bound yet» | The chat was never paired | Do [installation, step 6](installation.md#step-6-connect-your-chat) |
| The bot does not answer `/start` | The webhook is not set, the key is wrong, or the limit of chats is reached (the bot then answers «досягнуто ліміту чатів») | Check that `setWebhook` returned `ok`; use the `TelegramPairingKey` from Key Vault; to free a place, delete the row of a chat you no longer need (step 6) or raise `TelegramMaxChats`. Wrong keys are never answered, look for «Pairing rejected» in the logs |
| `/settings` opens but its buttons do nothing | The webhook was registered for `message` updates only | Run `setWebhook` again with `allowed_updates=["message","callback_query"]` ([installation, step 6](installation.md#step-6-connect-your-chat)) |
| `/settings` says the values from the bot are ignored | Together they are invalid, for example after the App Settings changed | Open the groups and reset (↺) the values you changed; the log has «Settings overrides are ignored» with the reason |
| A channel added in the bot stays ⏳ | It has not been collected successfully yet, or the reading account cannot see it | Wait for the next collection; if it fails the bot says so and offers «🗑 Видалити». Join the channel with the reading account |
| The bot does not answer a question | No digest yet (the bot says so), the chat is a group, the message started with `/`, or the answer failed (the bot says «Не вдалося відповісти») | Ask in the private chat after a digest; for a failure, ask again and look for «The chat could not answer» in the log |
| A question is never answered, no message at all | The question waited in `chat-messages` for over an hour, or a new digest arrived while it was being answered | Ask again; the answer to a question asked before a new digest is dropped on purpose |
| No digest arrives | The schedule is in UTC, or the deploy failed | Check [the schedule](configuration.md#schedule) and the Actions run or the function app logs |
| No alerts arrive | The group is not in `AlertSources`, quiet hours are on, or the cooldown is active | Check `AlertSources`, `AlertQuietHours` and `AlertCooldownHours`. Posts held back by quiet hours go into the digest |
| Posts seem scored against the wrong topics | The topics under `My interests` in `scoring.md` do not match what you follow | Fix the list ([installation, step 1](installation.md#step-1-write-your-rules)) and deploy again |
| A setting seems ignored | A typo in the key name: unknown keys are silently ignored | Compare the name with the [settings reference](configuration.md#settings-reference) |
| The Telegram sign-in stops with `PHONE_NUMBER_INVALID` | Telegram rejected the number: it is a landline or not registered in Telegram, or it has a typo | Use a mobile number that can receive Telegram codes, in the form `+48123456789`. Update the `TF_TELEGRAM_PHONE` secret and run `infra` with apply, then sign in again |
| The Telegram sign-in prints `Logged in as ...` and then hangs, with no `Session uploaded` | The tool picks an Azure credential that never answers on this machine | Set `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` (it uses only your `az login`) and run it again |
| «⚠️ Не оцінено N постів» arrives with the digest | Scoring failed 5 times and the message went to the `to-score-poison` queue (most often the OpenAI key, quota or model) | Find the cause in Application Insights (`ScorerFunction` errors), and fix it. The posts already in `to-score-poison` are not retried; new posts are scored again once the cause is fixed |
| The log shows `Collection skipped: another collection is using the Telegram session` | A manual `collect` overlapped another collection (usually the hourly timer) | Nothing to do: the next run reads the messages. Start a manual `collect` away from the collector schedule |

## Known limitations

These are deliberate trade-offs or open edges, not bugs to report.

- **Duplicate messages after a failed send.** A single Telegram send is never repeated automatically, because after a timeout the message may already be delivered. If a send fails, the job fails, its posts stay unsent
  and the next run sends them again, so a digest or alert can arrive twice. We prefer a duplicate to a lost message. A long digest is sent as several messages, and the posts are marked as sent only after the last one,
  so if a later message fails, the next run sends the whole digest again. A send gets 30 seconds; the log line `Telegram send failed: N chars after M ms` shows how long it took.
- **Unscored posts stay `New`.** After 5 failed attempts a message lands in `to-score-poison`, and its posts stay `New` until cleanup removes them. The digest reports how many there are, but nothing retries them.
- **Posts in two bundles.** If a collection run broke off between writing the posts and moving the read position, and new messages arrived meanwhile, the same group messages can land in two bundles.
- **First-run flood waits.** See the symptom table above.
- **Fresh messages have no reactions yet,** so the prominence order inside a bundle is approximate at first.
- **A repeated alert.** If an alert went out but saving its "sent" mark failed, the next run can repeat it.
- **Storage code is tested by hand.** The storage repository is checked manually on Azurite, not by `dotnet test`.

## Still stuck

Open Application Insights for the function app and look at the failures and traces of the last run. Developers: [architecture](../developer-guide/architecture.md).
