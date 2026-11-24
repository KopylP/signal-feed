locals {
  function_roles = {
    blob  = { scope = azurerm_storage_account.main.id, role = "Storage Blob Data Owner" }
    queue = { scope = azurerm_storage_account.main.id, role = "Storage Queue Data Contributor" }
    table = { scope = azurerm_storage_account.main.id, role = "Storage Table Data Contributor" }
    vault = { scope = azurerm_key_vault.main.id, role = "Key Vault Secrets User" }
  }

  deployer_roles = {
    secrets = { scope = azurerm_key_vault.main.id, role = "Key Vault Secrets Officer" }
    blob    = { scope = azurerm_storage_account.main.id, role = "Storage Blob Data Contributor" }
  }
}

# Managed identity функції: доступ до даних і секретів без ключів.
resource "azurerm_role_assignment" "function" {
  for_each             = local.function_roles
  scope                = each.value.scope
  role_definition_name = each.value.role
  principal_id         = azurerm_function_app_flex_consumption.main.identity[0].principal_id
}

# Ви: задавати секрети в Key Vault і завантажувати сесію Telegram (TgLogin).
resource "azurerm_role_assignment" "deployer" {
  for_each             = local.deployer_roles
  scope                = each.value.scope
  role_definition_name = each.value.role
  principal_id         = var.deployer_object_id
}
