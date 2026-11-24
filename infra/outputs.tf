output "resource_group_name" {
  description = "Resource group з усіма ресурсами."
  value       = azurerm_resource_group.main.name
}

output "function_app_name" {
  description = "Ім'я function app для деплою коду."
  value       = azurerm_function_app_flex_consumption.main.name
}

output "storage_account_name" {
  description = "Storage account: його ім'я потрібне TgLogin."
  value       = azurerm_storage_account.main.name
}

output "key_vault_name" {
  description = "Key Vault, куди записуються секрети (az keyvault secret set)."
  value       = azurerm_key_vault.main.name
}
