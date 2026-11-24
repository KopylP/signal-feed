data "azurerm_client_config" "current" {}

locals {
  suffix           = substr(sha1("${data.azurerm_client_config.current.subscription_id}-${var.name_prefix}"), 0, 6)
  storage_name     = substr("st${replace(var.name_prefix, "-", "")}${local.suffix}", 0, 24)
  key_vault_name   = substr("kv-${var.name_prefix}-${local.suffix}", 0, 24)
  function_name    = "func-${var.name_prefix}-${local.suffix}"
  secret_names     = ["OpenAIApiKey", "TelegramBotToken", "TelegramApiHash"]
  secret_reference = { for name in local.secret_names : name => "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/${name}/)" }

  app_settings = merge(
    {
      # Identity-based підключення до storage: без ключів і рядків підключення.
      AzureWebJobsStorage__accountName = azurerm_storage_account.main.name

      Sources              = var.sources
      CollectorSchedule    = var.collector_schedule
      DigestSchedule       = var.digest_schedule
      AlertSchedule        = var.alert_schedule
      DigestSize           = var.digest_size
      InterestingThreshold = var.interesting_threshold
      ScoringBatchSize     = var.scoring_batch_size
      MaxPostChars         = var.max_post_chars
      MinPostChars         = var.min_post_chars
      RetentionDays        = var.retention_days
      MaxItemsPerGroup     = var.max_items_per_group
      MaxBundleChars       = var.max_bundle_chars
      AlertSources         = var.alert_sources
      AlertWindowHours     = var.alert_window_hours
      AlertCooldownHours   = var.alert_cooldown_hours
      AlertThreshold       = var.alert_threshold
      BurstMinPosts        = var.burst_min_posts
      AlertMaxPerRun       = var.alert_max_per_run
      AlertQuietHours      = var.alert_quiet_hours
      AlertTimeZone        = var.alert_time_zone
      OpenAIModel          = var.openai_model
      TelegramChatId       = var.telegram_chat_id
      TelegramApiId        = var.telegram_api_id
      TelegramPhone        = var.telegram_phone
      SessionBlobContainer = var.session_blob_container
    },
    # Секрети: лише посилання на Key Vault, значення в state не потрапляють.
    local.secret_reference,
  )
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.name_prefix}"
  location = var.location
}

# ---------- Storage: blob, таблиці, черга ----------

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
  name                 = "ChannelState"
  storage_account_name = azurerm_storage_account.main.name
}

resource "azurerm_storage_table" "posts" {
  name                 = "Posts"
  storage_account_name = azurerm_storage_account.main.name
}

resource "azurerm_storage_queue" "to_score" {
  name                 = "to-score"
  storage_account_name = azurerm_storage_account.main.name
}

# ---------- Key Vault (RBAC). Значення секретів Terraform не створює. ----------

resource "azurerm_key_vault" "main" {
  name                       = local.key_vault_name
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
}

# ---------- Моніторинг ----------

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${var.name_prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = 0.1
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${var.name_prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
}

# ---------- Function App (Flex Consumption) ----------

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

# ---------- Ролі для managed identity функції ----------

locals {
  function_principal_id = azurerm_function_app_flex_consumption.main.identity[0].principal_id
}

resource "azurerm_role_assignment" "function_blob_owner" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = local.function_principal_id
}

resource "azurerm_role_assignment" "function_queue" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = local.function_principal_id
}

resource "azurerm_role_assignment" "function_table" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = local.function_principal_id
}

resource "azurerm_role_assignment" "function_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = local.function_principal_id
}

# ---------- Ролі для вас (deployer) ----------

resource "azurerm_role_assignment" "deployer_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.deployer_object_id
}

resource "azurerm_role_assignment" "deployer_blob" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = var.deployer_object_id
}

# ---------- Бюджет ----------

resource "azurerm_consumption_budget_resource_group" "main" {
  name              = "budget-${var.name_prefix}"
  resource_group_id = azurerm_resource_group.main.id
  amount            = var.monthly_budget_usd
  time_grain        = "Monthly"

  time_period {
    start_date = formatdate("YYYY-MM-01'T'00:00:00'Z'", timestamp())
  }

  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThanOrEqualTo"
    contact_emails = [var.budget_alert_email]
  }

  notification {
    enabled        = true
    threshold      = 100
    operator       = "GreaterThanOrEqualTo"
    contact_emails = [var.budget_alert_email]
  }

  lifecycle {
    ignore_changes = [time_period]
  }
}
