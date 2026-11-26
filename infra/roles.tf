locals {
  function_roles = {
    blob  = { scope = azurerm_storage_account.main.id, role = "Storage Blob Data Owner" }
    queue = { scope = azurerm_storage_account.main.id, role = "Storage Queue Data Contributor" }
    table = { scope = azurerm_storage_account.main.id, role = "Storage Table Data Contributor" }
    vault = { scope = azurerm_key_vault.main.id, role = "Key Vault Secrets User" }
    # Wrap and unwrap with the session key only: no other key, no secret values.
    session_key = { scope = azurerm_key_vault_key.session.resource_versionless_id, role = "Key Vault Crypto Service Encryption User" }
  }

  deployer_roles = {
    secrets = { scope = azurerm_key_vault.main.id, role = "Key Vault Secrets Officer" }
    blob    = { scope = azurerm_storage_account.main.id, role = "Storage Blob Data Contributor" }
    table   = { scope = azurerm_storage_account.main.id, role = "Storage Table Data Contributor" }
    queue   = { scope = azurerm_storage_account.main.id, role = "Storage Queue Data Contributor" }
    # TgLogin encrypts the session it uploads.
    session_key = { scope = azurerm_key_vault_key.session.resource_versionless_id, role = "Key Vault Crypto Service Encryption User" }
  }
}

# The function's managed identity: access to data and secrets without keys.
resource "azurerm_role_assignment" "function" {
  for_each             = local.function_roles
  scope                = each.value.scope
  role_definition_name = each.value.role
  principal_id         = azurerm_function_app_flex_consumption.main.identity[0].principal_id
}

# You: set secrets in Key Vault, upload the Telegram session (TgLogin), reset the chat binding (ChatBinding table)
# and start a job on demand (tools/run-now.cs puts a message into the run-now queue).
resource "azurerm_role_assignment" "deployer" {
  for_each             = local.deployer_roles
  scope                = each.value.scope
  role_definition_name = each.value.role
  principal_id         = var.deployer_object_id
}

# Data-plane access for whoever runs Terraform, so that it can manage containers, tables and queues through Entra ID
# once the storage account keys are switched off (storage_use_azuread, shared_access_key_enabled). The deployer already
# holds the same roles, and a second identical assignment would fail, so it is skipped when you run Terraform yourself.
resource "azurerm_role_assignment" "terraform_storage" {
  for_each = data.azurerm_client_config.current.object_id == var.deployer_object_id ? {} : {
    blob  = "Storage Blob Data Contributor"
    table = "Storage Table Data Contributor"
    queue = "Storage Queue Data Contributor"
  }
  scope                = azurerm_storage_account.main.id
  role_definition_name = each.value
  principal_id         = data.azurerm_client_config.current.object_id
}

# Whoever runs Terraform (you locally, or the infra workflow) creates and rotates the session key in the RBAC-mode vault.
resource "azurerm_role_assignment" "terraform_keys" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Crypto Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}
