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
