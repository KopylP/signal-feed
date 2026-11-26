# ---------- Infrastructure ----------

variable "location" {
  description = "Azure region. Make sure Flex Consumption is available there: az functionapp list-flexconsumption-locations"
  type        = string
  default     = "westeurope"
}

variable "name_prefix" {
  description = "Resource name prefix: 3-11 lowercase letters or digits. Storage account and Key Vault names are built from it."
  type        = string
  default     = "signalfeed"

  validation {
    condition     = can(regex("^[a-z0-9]{3,11}$", var.name_prefix))
    error_message = "name_prefix must be 3-11 lowercase letters or digits."
  }
}

variable "dotnet_version" {
  description = ".NET version for dotnet-isolated on Flex Consumption."
  type        = string
  default     = "10.0"
}

variable "deployer_object_id" {
  description = "Object ID of your user (az ad signed-in-user show --query id -o tsv). Gets access to secrets and Blob."
  type        = string
}

variable "budget_alert_email" {
  description = "Email for budget alerts."
  type        = string
  sensitive   = true # personal data: keep it out of plan output
}

variable "monthly_budget_usd" {
  description = "Monthly budget for the resource group."
  type        = number
  default     = 5
}

variable "session_blob_container" {
  description = "Container with the Telegram session. Terraform creates it, the function reads from it."
  type        = string
  default     = "tg-session"
}

# ---------- Required App Settings (no defaults in code) ----------

variable "sources" {
  description = "Comma-separated usernames of channels and groups. Override in terraform.tfvars or with the TF_SOURCES variable in GitHub."
  type        = string
  default     = "badseo,needforseo,devakatalk,sneex_seo,SEOBAZA,seodre"
}

variable "telegram_chat_id" {
  description = "Optional override: numeric id of the chat for the digest and alerts. Leave empty to bind the chat by sending /start <TelegramPairingKey> to the bot."
  type        = string
  default     = ""
  sensitive   = true # personal data: keep it out of plan output
}

variable "telegram_api_id" {
  description = "api_id of the app from my.telegram.org."
  type        = string
  sensitive   = true # tied to the owner's Telegram account: keep it out of plan output
}

variable "alert_sources" {
  description = "Groups that can trigger an alert (comma-separated usernames). Channels never trigger alerts."
  type        = string
  default     = ""
}

# ---------- Schedules (CRON in UTC). Timer attributes read them, so the defaults live here ----------

variable "collector_schedule" {
  description = "CRON for post collection (UTC)."
  type        = string
  default     = "0 0 * * * *"
}

variable "digest_schedule" {
  description = "CRON for the daily digest (UTC)."
  type        = string
  default     = "0 0 8 * * *"
}

variable "alert_schedule" {
  description = "CRON for the alert check (UTC)."
  type        = string
  default     = "0 30 * * * *"
}

variable "daily_summary_schedule" {
  description = "CRON for the daily summary of the finished days (UTC); the cleanup of old data starts right after it."
  type        = string
  default     = "0 0 3 * * *"
}

# ---------- Other settings ----------

variable "app_settings" {
  description = <<-EOT
    Extra App Settings: any parameter from src/PostAgent.Functions/Shared/Settings/AgentOptions.cs
    (DigestSize, AlertThreshold, AlertQuietHours, OpenAIModel, RetentionDays, etc.).
    A parameter that is not set uses the default from code. Names must match AgentOptions:
    a typo in a name is not an error, the parameter is simply ignored.
  EOT
  type        = map(string)
  default     = {}
}
