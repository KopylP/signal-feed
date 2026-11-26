resource "azurerm_storage_account" "main" {
  name                            = local.storage_name
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  # Everything uses managed identity and RBAC, so the account keys are not needed: a leaked key would give full access.
  shared_access_key_enabled       = false
  default_to_oauth_authentication = true
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

# Tables go through the Azure Resource Manager (azapi), not azurerm_storage_table: with the account keys off the azurerm provider
# cannot even read a table, because it reads the table ACLs with a shared key (ADR-0019). Blobs and queues are fine through azurerm.
locals {
  tables = {
    channel_state = "ChannelState"
    chat_binding  = "ChatBinding"
    posts         = "Posts"
  }
}

resource "azapi_resource" "table" {
  for_each  = local.tables
  type      = "Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01"
  name      = each.value
  parent_id = "${azurerm_storage_account.main.id}/tableServices/default"
  body      = {}
}

# The tables already exist (made by azurerm before): adopt them, never recreate. Remove these blocks after the first apply.
import {
  for_each = local.tables
  to       = azapi_resource.table[each.key]
  id       = "${azurerm_storage_account.main.id}/tableServices/default/tables/${each.value}"
}

removed {
  from = azurerm_storage_table.channel_state
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_storage_table.chat_binding
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_storage_table.posts
  lifecycle {
    destroy = false
  }
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

# A /digest on its way to DigestRequestFunction. Terraform makes it, like every trigger queue: on Flex Consumption the scale controller reports
# "Error querying for queue scale status" for a queue that does not exist, and the host does not create it.
resource "azurerm_storage_queue" "digest_requests" {
  name               = "digest-requests"
  storage_account_id = azurerm_storage_account.main.id
}

# The queue was made by the app before Terraform got to it: adopt it, never recreate. Remove this block after the first apply.
import {
  to = azurerm_storage_queue.digest_requests
  id = "${azurerm_storage_account.main.id}/queueServices/default/queues/digest-requests"
}

# "The daily summaries are written": the only start of the cleanup (CleanupFunction).
resource "azurerm_storage_queue" "cleanup_requests" {
  name               = "cleanup-requests"
  storage_account_id = azurerm_storage_account.main.id
}

# Sign-ins started from the bot with /login (ADR-0016).
resource "azurerm_storage_queue" "tg_login" {
  name               = "tg-login"
  storage_account_id = azurerm_storage_account.main.id
}
