resource "azurerm_storage_account" "main" {
  name                            = local.storage_name
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  tags                            = local.tags
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

resource "azurerm_storage_table" "chat_binding" {
  name               = "ChatBinding"
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

resource "azurerm_storage_queue" "chat_messages" {
  name               = "chat-messages"
  storage_account_id = azurerm_storage_account.main.id
}

resource "azurerm_storage_container" "chat_state" {
  name                  = "chat-state"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "settings" {
  name                  = "settings"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_queue" "run_now" {
  name               = "run-now"
  storage_account_id = azurerm_storage_account.main.id
}

# Sign-ins started from the bot with /login (ADR-0016).
resource "azurerm_storage_queue" "tg_login" {
  name               = "tg-login"
  storage_account_id = azurerm_storage_account.main.id
}
