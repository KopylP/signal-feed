locals {
  # Timer attributes read the schedules, so their defaults live here, not in code.
  # Precedence: a key in var.app_settings, then the dedicated variable, then its default.
  schedules = {
    CollectorSchedule    = var.collector_schedule
    DigestSchedule       = var.digest_schedule
    AlertSchedule        = var.alert_schedule
    DailySummarySchedule = var.daily_summary_schedule
  }

  # Merge order: extra settings from var.app_settings first, then explicit values, schedules, secrets last (last wins).
  # Only the schedule keys can be overridden from var.app_settings: everything else managed here wins over it.
  app_settings = merge(
    var.app_settings,
    {
      # Identity-based storage connection: no keys or connection strings.
      AzureWebJobsStorage__accountName = azurerm_storage_account.main.name

      Sources              = var.sources
      AlertSources         = var.alert_sources
      TelegramApiId        = var.telegram_api_id
      SessionBlobContainer = var.session_blob_container
      # Versionless: wrapping uses the current version, each blob names the version that unwraps it (ADR-0017).
      SessionKeyId = azurerm_key_vault_key.session.versionless_id
    },
    # Optional override: without it the chat is bound by sending /start <TelegramPairingKey> to the bot.
    var.telegram_chat_id == "" ? {} : { TelegramChatId = var.telegram_chat_id },
    { for name, value in local.schedules : name => lookup(var.app_settings, name, value) },
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
  tags                = local.tags
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

  tags = local.tags
}

locals {
  # The complete App Settings collection: the PUT below replaces everything on the app, so every key must be here.
  # APPLICATIONINSIGHTS_CONNECTION_STRING is what azurerm derives from site_config; leaving it out would show as a
  # difference on the next plan.
  effective_app_settings = merge(local.app_settings, {
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.main.connection_string
  })
}

# azurerm (4.81.0 through 5.15.0) writes AzureWebJobsStorage with an empty AccountKey, and on updates also
# DEPLOYMENT_STORAGE_CONNECTION_STRING, on every create or update of a Flex Consumption app, and hides both from
# plan (hashicorp/terraform-provider-azurerm#29149). The host prefers that string over AzureWebJobsStorage__accountName
# and gets 403 on timers, keys and queue triggers. Right after any change to the app, write back exactly our settings.
# Remove this action once the provider stops injecting the settings.
resource "azapi_resource_action" "app_settings" {
  type        = "Microsoft.Web/sites/config@2024-04-01"
  resource_id = "${azurerm_function_app_flex_consumption.main.id}/config/appsettings"
  method      = "PUT"
  # Key Vault references and non-secret values only, but sensitive so the plan summary of the public repository hides them.
  body = sensitive({ properties = local.effective_app_settings })

  lifecycle {
    # The whole resource, not only app_settings: any update of the app injects the settings again.
    replace_triggered_by = [azurerm_function_app_flex_consumption.main]
  }
}
