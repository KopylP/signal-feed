data "azurerm_client_config" "current" {}

locals {
  # Globally unique names get a stable suffix derived from the subscription and the prefix.
  suffix         = substr(sha1("${data.azurerm_client_config.current.subscription_id}-${var.name_prefix}"), 0, 6)
  storage_name   = substr("st${var.name_prefix}${local.suffix}", 0, 24)
  key_vault_name = substr("kv-${var.name_prefix}-${local.suffix}", 0, 24)
  function_name  = "func-${var.name_prefix}-${local.suffix}"
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.name_prefix}"
  location = var.location
}
