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

  depends_on = [azurerm_role_assignment.terraform_keys]
}
