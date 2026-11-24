locals {
  # Merge order: extra settings from var.app_settings first, then explicit values, secrets last (last wins).
  app_settings = merge(
    var.app_settings,
    {
      # Identity-based storage connection: no keys or connection strings.
      AzureWebJobsStorage__accountName = azurerm_storage_account.main.name

      Sources              = var.sources
      AlertSources         = var.alert_sources
      TelegramChatId       = var.telegram_chat_id
      TelegramApiId        = var.telegram_api_id
      TelegramPhone        = var.telegram_phone
      SessionBlobContainer = var.session_blob_container

      # Timer attributes read the schedules, so their defaults live here, not in code.
      CollectorSchedule = var.collector_schedule
      DigestSchedule    = var.digest_schedule
      AlertSchedule     = var.alert_schedule
      CleanupSchedule   = var.cleanup_schedule
    },
    # Secrets: Key Vault references only, the values never reach the state.
    local.secret_reference,
  )
}

resource "azurerm_service_plan" "main" {
  name                = "plan-${var.name_prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "FC1"
}

resource "azurerm_function_app_flex_consumption" "main" {
  name                = local.function_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  storage_container_type      = "blobContainer"
  storage_container_endpoint  = "${azurerm_storage_account.main.primary_blob_endpoint}${azurerm_storage_container.deployment.name}"
  storage_authentication_type = "SystemAssignedIdentity"

  runtime_name           = "dotnet-isolated"
  runtime_version        = var.dotnet_version
  instance_memory_in_mb  = 512
  maximum_instance_count = 10

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_insights_connection_string = azurerm_application_insights.main.connection_string
  }

  app_settings = local.app_settings
}
