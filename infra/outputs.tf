output "resource_group_name" {
  description = "Resource group that holds all resources."
  value       = azurerm_resource_group.main.name
}

output "function_app_name" {
  description = "Function app name, used to deploy the code."
  value       = azurerm_function_app_flex_consumption.main.name
}

output "storage_account_name" {
  description = "Storage account name, needed by TgLogin."
  value       = azurerm_storage_account.main.name
}

output "key_vault_name" {
  description = "Key Vault that receives the secrets (az keyvault secret set)."
  value       = azurerm_key_vault.main.name
}
