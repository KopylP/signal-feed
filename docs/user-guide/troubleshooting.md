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
| No digest arrives | The schedule is in UTC, or the deploy failed | Check [the schedule](configuration.md#schedule) and the Actions run or the function app logs |
| No alerts arrive | The group is not in `AlertSources`, quiet hours are on, or the cooldown is active | Check `AlertSources`, `AlertQuietHours` and `AlertCooldownHours`. Posts held back by quiet hours go into the digest |
| Posts seem scored against nonsense | The placeholders in `scoring.md` were never replaced | Write your topics and examples ([installation, step 1](installation.md#step-1-write-your-rules)) and deploy again |
| A setting seems ignored | A typo in the key name: unknown keys are silently ignored | Compare the name with the [settings reference](configuration.md#settings-reference) |
| Posts stay unscored | Scoring failed 5 times and the message went to the `to-score-poison` queue | Open the queue in the Azure portal and find the cause in Application Insights. There is no automatic notification |

## Known limitations

These are deliberate trade-offs or open edges, not bugs to report.

- **Duplicate messages on retry.** If Telegram answered `sendMessage` with a delay, a retry can produce a duplicate digest or alert. We prefer a duplicate to a lost message.
- **Unscored posts stay `New`.** After 5 failed attempts a message lands in `to-score-poison`, and its posts stay `New` until cleanup removes them.
- **Posts in two bundles.** If a collection run broke off between writing the posts and moving the read position, and new messages arrived meanwhile, the same group messages can land in two bundles.
- **First-run flood waits.** See the symptom table above.
- **Fresh messages have no reactions yet,** so the prominence order inside a bundle is approximate at first.
- **A repeated alert.** If an alert went out but saving its "sent" mark failed, the next run can repeat it.
- **Storage code is tested by hand.** The storage repository is checked manually on Azurite, not by `dotnet test`.

## Still stuck

Open Application Insights for the function app and look at the failures and traces of the last run. Developers: [architecture](../developer-guide/architecture.md).
