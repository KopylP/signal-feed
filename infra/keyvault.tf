# Key Vault in RBAC mode. Terraform does not create secret values, so they never end up in the state.
resource "azurerm_key_vault" "main" {
  name                       = local.key_vault_name
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
}

locals {
  # Secrets the app reads through Key Vault references (values are set manually, see docs/user-guide/installation.md).
  secret_names     = ["OpenAIApiKey", "TelegramBotToken", "TelegramApiHash", "TelegramPairingKey", "TelegramWebhookSecret"]
  secret_reference = { for name in local.secret_names : name => "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/${name}/)" }
}

# The key that encrypts the Telegram session and the sign-in in progress (ADR-0017). Key Vault generates it and the private
# part never leaves the vault, so nothing secret reaches the state. The app only wraps and unwraps data keys with it.
# Deleting it makes the stored session unreadable: sign in again with /login or TgLogin.
resource "azurerm_key_vault_key" "session" {
  name         = "tg-session"
  key_vault_id = azurerm_key_vault.main.id
  key_type     = "RSA"
  key_size     = 3072
  key_opts     = ["wrapKey", "unwrapKey"]

  # Key Vault makes a new version on its own every 90 days; no function or pipeline is needed. Old versions stay enabled,
  # so blobs wrapped with them can still be read, and every rewrite moves a blob to the newest version.
  # A version expires a year after it is created: from then it no longer wraps, but it still unwraps.
  rotation_policy {
    automatic {
      time_after_creation = "P90D"
    }
    expire_after         = "P1Y"
    notify_before_expiry = "P30D"
  }

  depends_on = [azurerm_role_assignment.terraform_keys]
}
