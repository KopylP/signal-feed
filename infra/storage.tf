resource "azurerm_storage_account" "main" {
  name                            = local.storage_name
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
}

resource "azurerm_storage_container" "deployment" {
  name                  = "deployment-package"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "session" {
  name                  = var.session_blob_container
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_table" "channel_state" {
  name               = "ChannelState"
  storage_account_id = azurerm_storage_account.main.id
}

resource "azurerm_storage_table" "posts" {
  name               = "Posts"
  storage_account_id = azurerm_storage_account.main.id
}

resource "azurerm_storage_queue" "to_score" {
  name               = "to-score"
  storage_account_id = azurerm_storage_account.main.id
}
