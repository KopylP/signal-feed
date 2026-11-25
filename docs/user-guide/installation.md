# Installation

**Audience:** an operator installing their own copy in Azure. Allow about an hour.
Want to try it on your computer first? See [local development](../developer-guide/local-development.md).

## Before you start

You need the accounts and keys listed in the [overview](overview.md#what-you-need), plus these tools on your machine:

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), signed in with `az login`
- [Terraform](https://developer.hashicorp.com/terraform/install) 1.5 or newer
- [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)

Check that the Flex Consumption plan is available in your region (and change `location` in `terraform.tfvars` if it is not):

```bash
az functionapp list-flexconsumption-locations -o table
```

The installation has six steps: your rules, the Azure resources, the secrets, the Telegram sign-in, the code, the chat.

## Step 1. Write your rules

Open [`prompts/scoring.md`](../../prompts/scoring.md) and check the list under `My interests`: the model scores every post against it. Replace the topics with your own if they differ.
Real examples of good and weak posts make the scoring sharper; you can add them under a `## Examples` heading.
The prompts are English, but they tell the model to write the topic, reason and digest text in Ukrainian, because the digest is read in Ukrainian.
Prompts ship with the build, so changing them later needs a new deploy.

## Step 2. Create the Azure resources

```bash
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
cd infra
cp terraform.tfvars.example terraform.tfvars   # fill in, notably deployer_object_id and budget_alert_email
terraform init
terraform apply
```

- `deployer_object_id` is your own identity: `az ad signed-in-user show --query id -o tsv`.
- State is local by default. For remote state see `backend.tf.example`.

Terraform creates one resource group with everything inside: storage, a Key Vault, monitoring, the function app with its identity and roles, and the $5 budget.
The full list is in the [architecture](../developer-guide/architecture.md#azure-resources).

## Step 3. Store the secrets in Key Vault

Terraform deliberately does not create the secret values, so they never end up in its state:

```bash
KV=$(terraform output -raw key_vault_name)
read -rsp "OpenAI API key: " V && az keyvault secret set --vault-name $KV --name OpenAIApiKey --value "$V" >/dev/null; echo
read -rsp "Telegram bot token: " V && az keyvault secret set --vault-name $KV --name TelegramBotToken --value "$V" >/dev/null; echo
read -rsp "Telegram api_hash: " V && az keyvault secret set --vault-name $KV --name TelegramApiHash --value "$V" >/dev/null; echo
# two random values you do not have to remember: they connect your chat in step 6
az keyvault secret set --vault-name $KV --name TelegramPairingKey --value "$(openssl rand -hex 16)" >/dev/null
az keyvault secret set --vault-name $KV --name TelegramWebhookSecret --value "$(openssl rand -hex 32)" >/dev/null
```

The app reads them through Key Vault references. If a reference is not picked up, check that the function's identity has the `Key Vault Secrets User` role and restart the app (`az functionapp restart`).
An unresolved reference stops the app at startup with an error that names the secret.

## Step 4. Sign in to Telegram

Use the separate account, not your main one. The `api_id` and `api_hash` come from [my.telegram.org](https://my.telegram.org).
If a group cannot be read, join it with this account.

```bash
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...   # a mobile number registered in Telegram, no spaces
export AZURE_TOKEN_CREDENTIALS=AzureCliCredential                         # use only your az login (see Troubleshooting if the tool hangs)
dotnet run --project src/TgLogin -- $(terraform -chdir=infra output -raw storage_account_name)
```

The tool asks for the Telegram code and the two-factor password, then uploads the session to the `tg-session` container using your `az login`
(Terraform already granted you the Storage Blob Data Contributor role). It ends with `Session uploaded`. When the session expires, repeat this step.

## Step 5. Deploy the code

```bash
dotnet publish src/PostAgent.Functions -c Release -o publish
cd publish
func azure functionapp publish $(terraform -chdir=../infra output -raw function_app_name)
```

## Step 6. Connect your chat

The agent does not need your chat id: you pair the chat by writing to the bot. First tell Telegram where to send the bot's messages (once, after the code is deployed):

```bash
KV=$(terraform -chdir=infra output -raw key_vault_name)
secret() { az keyvault secret show --vault-name $KV --name "$1" --query value -o tsv; }
HOST=$(az functionapp show -g $(terraform -chdir=infra output -raw resource_group_name) \
  -n $(terraform -chdir=infra output -raw function_app_name) --query defaultHostName -o tsv)
curl -s "https://api.telegram.org/bot$(secret TelegramBotToken)/setWebhook" \
  --data-urlencode "url=https://$HOST/api/telegram/webhook" \
  --data-urlencode "secret_token=$(secret TelegramWebhookSecret)" \
  --data-urlencode 'allowed_updates=["message","callback_query"]'
echo "https://t.me/<your_bot_username>?start=$(secret TelegramPairingKey)"
```

`callback_query` is what makes the buttons of `/settings` work. If you installed the webhook earlier with `["message"]` only, run this call again. `setWebhook` answers `{"ok":true,...}`. Open the printed link on the phone or desktop where you want the digest, press **Start**, and the bot replies «✅ Чат прив'язано».
(Typing `/start <key>` to the bot works too.) Every chat that sends the right key is added (up to `TelegramMaxChats`, 5 by default) and gets the same digest and alerts; each chat has its own conversation and menu, and the settings are shared ([ADR-0014](../adr/0014-serve-several-chats-with-shared-settings.md)). Wrong keys are ignored.

To add a second chat (a test account, a colleague), send it the same link. To move the digest to another chat, delete the binding of the old one and pair the new one; `<chat id>` is the number the row key shows in the `ChatBinding` table (a row written by an earlier version has the key `digest`):

```bash
az storage entity delete --account-name $(terraform -chdir=infra output -raw storage_account_name) --auth-mode login \
  --table-name ChatBinding --partition-key chat --row-key <chat id>
```

Prefer a fixed chat? Set `telegram_chat_id` instead (see [Configuration](configuration.md)): pairing is then off and exactly that one chat is served.

## What to expect

- The collector runs every hour, the digest arrives once a day (08:00 UTC by default), and cleanup runs at 03:00 UTC. Times are UTC: see [Configuration](configuration.md#schedule).
- Until you pair the chat (step 6), the digest and alerts cannot be sent: each run fails and its posts wait for the next one.
- Until the first collection the digest has nothing to send. If an hourly «Збір не вдався» ("Collection failed") message arrives, go to [troubleshooting](troubleshooting.md).

## Automatic deployment (optional)

To deploy on every push to `main` instead of by hand, set up GitHub Actions. That is a developer topic: [CI/CD](../developer-guide/ci-cd.md).

## Next

[Configure](configuration.md) your sources, schedules and thresholds.
