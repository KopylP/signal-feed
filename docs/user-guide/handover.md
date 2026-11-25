# Handing the agent over to another person

**Audience:** the operator who moves an installed agent to a new owner: the digest goes to their chat and, if needed, their Telegram account reads the channels.
Allow about 30 minutes. The Azure resources, the code and the sources stay as they are.

## What belongs to a person

| Part | Where it lives | Moves with the owner? |
|---|---|---|
| Chat for the digest and alerts | table `ChatBinding`, or `telegram_chat_id` | Always |
| Reader account (reads channels) | blob in `tg-session`, Key Vault `TelegramApiHash`, `telegram_api_id` | Only if the new owner brings their own account |
| Bot | Key Vault `TelegramBotToken` (from @BotFather) | Only if the bot must belong to the new owner |
| Old posts, read positions, conversation, bot-made settings | tables `Posts`, `ChannelState`, containers `chat-state`, `settings`, queues `to-score`, `chat-messages` | Cleared, so the new owner starts clean |

Decide first: **minimal** (only the chat changes, the reader account and bot stay yours) or **full** (reader account and bot change too). Steps marked *full only* are skipped in the minimal case.

## Before you start

```bash
cd infra
SA=$(terraform output -raw storage_account_name)
KV=$(terraform output -raw key_vault_name)
RG=$(terraform output -raw resource_group_name)
APP=$(terraform output -raw function_app_name)
```

## Step 1. Cut off the old chat

Deleting the bindings stops the digest to the old chats right away. Runs that happen before the new pairing fail and are retried, which is expected.
There can be several bindings (one row per chat, [ADR-0014](../adr/0014-serve-several-chats-with-shared-settings.md)); delete the ones that must go. To keep a chat of your own for tests, leave its row.

```bash
az storage entity query --account-name $SA --auth-mode login --table-name ChatBinding --select RowKey --query "items[].RowKey" -o tsv
az storage entity delete --account-name $SA --auth-mode login --table-name ChatBinding --partition-key chat --row-key <row key>
```

If you use a fixed chat (`telegram_chat_id` in `terraform.tfvars`, or the `TF_TELEGRAM_CHAT_ID` secret in GitHub), remove it: pairing is off while it is set.

## Step 2. New credentials (full only)

1. **Bot.** In @BotFather the new owner creates a bot (or you transfer yours) and gives you the token. Store it:
   `read -rsp "Bot token: " V && az keyvault secret set --vault-name $KV --name TelegramBotToken --value "$V" >/dev/null; echo`.
   Revoke the old token in @BotFather if the old owner keeps the old bot.
2. **Reader account.** The new owner uses a separate Telegram account (not their main one) and takes `api_id` and `api_hash` from [my.telegram.org](https://my.telegram.org).
   Store the hash in Key Vault as `TelegramApiHash`. Set the new `telegram_api_id` in `terraform.tfvars`
   (or the `TF_TELEGRAM_API_ID` secret in GitHub) and run `terraform apply` (or the `infra` workflow).
3. **Session.** Remove the old one and sign in with the new account, as in [installation, step 4](installation.md#step-4-sign-in-to-telegram):
   ```bash
   az storage blob delete-batch --account-name $SA --auth-mode login -s tg-session
   export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380... AZURE_TOKEN_CREDENTIALS=AzureCliCredential
   export SESSION_KEY_ID=$(terraform -chdir=infra output -raw session_key_id)
   dotnet run --project src/TgLogin -- $SA
   ```
   Or, once the new owner's chat is paired (step 3 below), they send `/login` to the bot and type the new account's number and code: the new session replaces the old one
   ([Renewing the Telegram session](how-it-works.md#renewing-the-telegram-session)).
4. The old owner opens Telegram → Settings → Devices and ends the Signal Feed session.
5. Join the groups that the new account cannot read yet.

Also rotate the session key, so that a copy of the old session someone kept cannot be opened with it later
([Rotating the session key](configuration.md#rotating-the-session-key)).

## Step 3. New pairing key

The old pairing link must stop working. Rotate the key, even in the minimal case:

```bash
az keyvault secret set --vault-name $KV --name TelegramPairingKey --value "$(openssl rand -hex 16)" >/dev/null
az functionapp restart -g $RG -n $APP
```

## Step 4. Clear the history

Wipe the data of the old owner. The next `collect` then reads `FirstRunLookbackHours` (24 by default) of every source, which is what the first report is built from.

```bash
# tables: delete the rows and keep the tables (a query returns up to 1000 rows, so repeat until it prints 0)
for T in Posts ChannelState; do
  az storage entity query --account-name $SA --auth-mode login --table-name $T --select PartitionKey,RowKey \
    --query "items[].[PartitionKey,RowKey]" -o tsv |
  while IFS=$'\t' read -r PK RK; do
    az storage entity delete --account-name $SA --auth-mode login --table-name $T --partition-key "$PK" --row-key "$RK"
  done
  echo "$T rows left: $(az storage entity query --account-name $SA --auth-mode login --table-name $T --query 'length(items)' -o tsv)"
done
# queues
for Q in to-score chat-messages; do az storage message clear --account-name $SA --auth-mode login --queue-name $Q; done
# conversation about the last digest
az storage blob delete-batch --account-name $SA --auth-mode login -s chat-state
```

Deleting row by row is slow when a table holds thousands of rows, but the tables stay in place.

Settings and sources the old owner changed in the bot (`/settings`, or by writing to the agent) live in the `settings` container.
To give the new owner the defaults from Terraform and App Settings, clear it as well; to keep the current setup, skip this:

```bash
az storage blob delete-batch --account-name $SA --auth-mode login -s settings
```

## Step 5. Pair the new chat

Only needed after a *full* change of bot, and the first time in general: point the bot to the webhook (the command is in [installation, step 6](installation.md#step-6-connect-your-chat)).
Then send the printed link to the new owner. They open it, press **Start**, and the bot answers «✅ Чат прив'язано».
Every chat with the key is added (up to `TelegramMaxChats`), so send the link only to the people who should get the digest, and pair your own test chat the same way with your own `/start` link.
Change `TelegramPairingKey` again afterwards if the link must not work any more.

## Step 6. Ask for the first report

Manual runs are off by default. Turn them on, run the two jobs in order, and turn them off:

```bash
az functionapp config appsettings set -g $RG -n $APP --settings ManualRunsEnabled=true >/dev/null   # restarts the app
dotnet run tools/run-now.cs -- collect --storage $SA
# wait for the collection (a minute or two), then check the log for RunNowFunction; scoring happens in the to-score queue
dotnet run tools/run-now.cs -- digest --storage $SA
az functionapp config appsettings set -g $RG -n $APP --settings ManualRunsEnabled=false >/dev/null
```

The digest arrives in the new chat. If it says there are no posts, scoring has not finished yet: wait a few minutes and run `digest` again.
Otherwise the regular schedule takes over (see [Configuration](configuration.md#schedule)).

## Checklist

- [ ] Old chats unbound, no fixed `telegram_chat_id` left (step 1)
- [ ] Full only: new bot token, `api_hash`, `api_id`, phone, new session; old session ended (step 2)
- [ ] Pairing key rotated (step 3)
- [ ] `Posts`, `ChannelState`, queues and `chat-state` cleared (step 4)
- [ ] New chat paired (step 5)
- [ ] First digest received, `ManualRunsEnabled` back to `false` (step 6)

If something does not arrive, see [troubleshooting](troubleshooting.md).
