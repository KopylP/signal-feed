# Troubleshooting

**Audience:** operators. Find your symptom, read the cause, apply the fix. Known limitations are at the end.

## Symptoms

| Symptom | Likely cause | What to do |
|---|---|---|
| The app does not start; the log names a setting | Startup validation failed: a required value is missing or out of range | Fix the setting named in the log. See [Validation](configuration.md#validation) |
| The app does not start and the log says `Connection string has keyword 'AccountKey' with empty value` | An App Setting `AzureWebJobsStorage` (a connection string without a key) exists next to `AzureWebJobsStorage__accountName`. The Terraform provider writes it on every update of a Flex Consumption app; the `infra` workflow now overwrites it and fails its check step if it survives | Run `infra` with `apply` again. As a stopgap delete it: `az functionapp config appsettings delete -g <rg> -n <app> --setting-names AzureWebJobsStorage DEPLOYMENT_STORAGE_CONNECTION_STRING`. The same cause breaks timers (`The listener for function ... was unable to start`, 403) |
| Startup fails with an error naming a secret | A Key Vault reference is unresolved | Check that the function's identity has the `Key Vault Secrets User` role and that the secret exists. Then run `az functionapp restart` |
| «Збір не вдався» ("Collection failed") arrives every hour | Collection fails, most often because the Telegram session expired (`Telegram session is expired`) | Send `/login` to the bot ([Renewing the Telegram session](how-it-works.md#renewing-the-telegram-session)), or run `TgLogin` ([installation, step 4](installation.md#step-4-sign-in-to-telegram)). The message repeats hourly until the cause is fixed |
| `/login` gets no answer | The chat is not a paired private chat, or the `tg-login` queue does not exist (the log has «Handling the Telegram update failed») | Send it from the paired private chat; run the `infra` workflow with apply so that the queue exists |
| `/login` says «Вхід через бота тут недоступний» | The app does not know its own host name (`WEBSITE_HOSTNAME`), for example when it runs locally | Sign in with `TgLogin` |
| `/login` says «Зараз іде збір, сесія зайнята» | A collection held the session for over two minutes | Send `/login` again in a minute |
| The form says «Форма застаріла» or «Цю спробу завершено» | The sign-in expired (5 minutes), was finished, or the form was opened from another account or chat | Send `/login` again and use the newest button |
| «Сесію акаунта-читача оновлено» arrives and you did not sign in | Someone in another paired chat signed in with `/login` | If the account is not yours, send `/login` and sign in with your account; unbind chats you do not trust ([installation, step 6](installation.md#step-6-connect-your-chat)) |
| `/login` ends with «Telegram просить те, чого бот не вміє» | Telegram wants a sign-up or a login email for this account | Sign in with `TgLogin` |
| One group or channel never appears | The reading account cannot see it | Join it with the reading account |
| A new busy group is skipped on the first run | Reading a day of history means many requests, and Telegram answered `FLOOD_WAIT` | Nothing: the source is skipped and retried on the next run |
| No digest or alert arrives, the log says «No Telegram chat is bound yet» | The chat was never paired | Do [installation, step 6](installation.md#step-6-connect-your-chat) |
| The bot does not answer `/start` | The webhook is not set, the key is wrong, or the limit of chats is reached (the bot then answers «досягнуто ліміту чатів») | Check that `setWebhook` returned `ok`; use the `TelegramPairingKey` from Key Vault; to free a place, delete the row of a chat you no longer need (step 6) or raise `TelegramMaxChats`. Wrong keys are never answered, look for «Pairing rejected» in the logs |
| `/settings` opens but its buttons do nothing | The webhook was registered for `message` updates only | Run `setWebhook` again with `allowed_updates=["message","callback_query"]` ([installation, step 6](installation.md#step-6-connect-your-chat)) |
| `/settings` says the values from the bot are ignored | Together they are invalid, for example after the App Settings changed | Open the groups and reset (↺) the values you changed; the log has «Settings overrides are ignored» with the reason |
| A channel added in the bot stays ⏳ | It has not been collected successfully yet, or the reading account cannot see it | Wait for the next collection; if it fails the bot says so and offers «🗑 Видалити». Join the channel with the reading account |
| The answer shows «💭 Думаю» or a partial text and then stops, or it behaves oddly while it is written | The live preview (a Telegram draft) is misbehaving; the final answer is not affected | Set `ChatStreamEnabled` to `false` (the answer then arrives at once) and tell the maintainer; look for «The answer preview stopped» in the log |
| The bot does not answer a question | The chat is a group, the message started with `/` (the commands are `/settings`, `/digest`, `/stats`, `/clear`, `/login` and `/start`; ask the agent «які є команди?»), or the answer failed (the bot says «Не вдалося відповісти») | Ask in the private chat; for a failure, ask again and look for «The chat could not answer» in the log |
| A question is never answered, no message at all | The question waited in `chat-messages` for over an hour, or a new digest arrived while it was being answered | Ask again; the answer to a question asked before a new digest is dropped on purpose |
| The agent says a rating was not saved, or rating does nothing | `FeedbackEnabled` is `false` (the agent says rating is switched off), the post is older than `RetentionDays`, or the storage failed («A vote could not be saved» in Application Insights) | Turn `FeedbackEnabled` on; rate a newer post; for a storage failure, ask again |
| The agent saved something wrong about you, or keeps something you did not want | It decides on its own what to keep (a line «🧠 …» under the answer shows every change) | Tell it «забудь …» or correct the fact; ask «що ти про мене пам'ятаєш?» to check. The notes are also blobs under `settings/reader-memory/<chat id>/`, which you can fix or delete in the portal |
| The agent says the memory is full | 10 notes is the limit | Nothing: it merges or removes a note itself; you can also tell it what to forget |
| `/stats` says there is no data, or the agent has no trends | The first summary is written by the daily summary job after the first full day (about 03:00 UTC); `/stats` itself covers the days still stored | Wait a day, or check that `DailySummarySchedule` runs and look for «Daily summaries» in Application Insights |
| Old posts and conversations are not deleted, or `/stats` stays empty | The daily summary keeps failing, so no cleanup request is queued (the cleanup never runs without it) | Look for «Daily summaries failed» and failures of `DailySummaryFunction` in Application Insights; check that the function identity can write blobs in the `settings` container; run `daily-summary` through run-now to retry |
| No digest arrives | The schedule is in UTC, or the deploy failed | Check [the schedule](configuration.md#schedule) and the Actions run or the function app logs |
| No alerts arrive | The group is not in `AlertSources`, quiet hours are on, or the cooldown is active | Check `AlertSources`, `AlertQuietHours` and `AlertCooldownHours`. Posts held back by quiet hours go into the digest |
| Posts seem scored against the wrong topics | The topics under `My interests` in `scoring.md` do not match what you follow | Fix the list ([installation, step 1](installation.md#step-1-write-your-rules)) and deploy again |
| A setting seems ignored | A typo in the key name: unknown keys are silently ignored | Compare the name with the [settings reference](configuration.md#settings-reference) |
| The Telegram sign-in stops with `PHONE_NUMBER_INVALID` | Telegram rejected the number: it is a landline or not registered in Telegram, or it has a typo | Use a mobile number that can receive Telegram codes, in the form `+48123456789`, and sign in again (with `TgLogin`, fix `TELEGRAM_PHONE`) |
| The app does not start: «Set SessionKeyId» | The app runs in Azure without the Key Vault key that encrypts the session | Run the `infra` workflow with apply: it creates the key and sets `SessionKeyId` |
| `infra` apply fails creating `azurerm_key_vault_key.session` with 403 | The "Key Vault Crypto Officer" role that Terraform has just granted itself has not propagated yet | Run apply again in a few minutes |
| Collection or `/login` fails with a 403 from Key Vault | The function's identity lost the "Key Vault Crypto Service Encryption User" role on the `tg-session` key, or the key is disabled | Run `infra` with apply; if the key was deleted, the stored session cannot be read: sign in again with `/login` |
| `TgLogin` says «Set SESSION_KEY_ID» | The session is stored encrypted and the tool needs the key | `export SESSION_KEY_ID=$(terraform -chdir=infra output -raw session_key_id)` and run it again |
| The Telegram sign-in prints `Logged in as ...` and then hangs, with no `Session uploaded` | The tool picks an Azure credential that never answers on this machine | Set `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` (it uses only your `az login`) and run it again |
| «⚠️ Не оцінено N постів» arrives with the digest | Scoring failed 5 times and the message went to the `to-score-poison` queue (most often the OpenAI key, quota or model) | Find the cause in Application Insights (`ScorerFunction` errors), and fix it. The posts already in `to-score-poison` are not retried; new posts are scored again once the cause is fixed |
| The log shows `Collection skipped: another collection or a sign-in is using the Telegram session` | A manual `collect` overlapped another collection (usually the hourly timer), or a `/login` was running | Nothing to do: the next run reads the messages. Start a manual `collect` away from the collector schedule |

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
