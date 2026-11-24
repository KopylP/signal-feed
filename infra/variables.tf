# ---------- Інфраструктура ----------

variable "location" {
  description = "Регіон Azure. Перевірте, що Flex Consumption доступний: az functionapp list-flexconsumption-locations"
  type        = string
  default     = "westeurope"
}

variable "name_prefix" {
  description = "Префікс імен ресурсів: 3-11 малих літер і цифр. З нього будуються імена storage account і Key Vault."
  type        = string
  default     = "signalfeed"

  validation {
    condition     = can(regex("^[a-z0-9]{3,11}$", var.name_prefix))
    error_message = "name_prefix must be 3-11 lowercase letters or digits."
  }
}

variable "dotnet_version" {
  description = "Версія .NET для dotnet-isolated на Flex Consumption."
  type        = string
  default     = "10.0"
}

variable "deployer_object_id" {
  description = "Object ID вашого користувача (az ad signed-in-user show --query id -o tsv). Отримує роботу з секретами й Blob."
  type        = string
}

variable "budget_alert_email" {
  description = "Email для сповіщень про бюджет."
  type        = string
}

variable "monthly_budget_usd" {
  description = "Місячний бюджет на resource group."
  type        = number
  default     = 5
}

variable "session_blob_container" {
  description = "Контейнер із сесією Telegram. Terraform створює його, а функція читає з нього."
  type        = string
  default     = "tg-session"
}

# ---------- Обов'язкові App Settings (без значень за замовчуванням у коді) ----------

variable "sources" {
  description = "Username каналів і груп через кому. Перевизначається в terraform.tfvars або змінною TF_SOURCES у GitHub."
  type        = string
  default     = "badseo,needforseo,devakatalk,sneex_seo,SEOBAZA,seodre"
}

variable "telegram_chat_id" {
  description = "Куди слати дайджест і алерти (числовий id чату)."
  type        = string
}

variable "telegram_api_id" {
  description = "api_id застосунку з my.telegram.org."
  type        = string
}

variable "telegram_phone" {
  description = "Номер телефону акаунта-читача, формат +380..."
  type        = string
}

variable "alert_sources" {
  description = "Групи, які можуть викликати алерт (username через кому). Канали алертів не викликають."
  type        = string
  default     = ""
}

# ---------- Розклади (CRON у UTC). Їх читають атрибути таймерів, тому значення за замовчуванням тут ----------

variable "collector_schedule" {
  description = "CRON збору постів (UTC)."
  type        = string
  default     = "0 0 * * * *"
}

variable "digest_schedule" {
  description = "CRON денного дайджесту (UTC)."
  type        = string
  default     = "0 0 8 * * *"
}

variable "alert_schedule" {
  description = "CRON перевірки алертів (UTC)."
  type        = string
  default     = "0 30 * * * *"
}

variable "cleanup_schedule" {
  description = "CRON очищення старих даних (UTC)."
  type        = string
  default     = "0 0 3 * * *"
}

# ---------- Решта налаштувань ----------

variable "app_settings" {
  description = <<-EOT
    Додаткові App Settings: будь-які параметри з src/PostAgent.Functions/Shared/Settings/AgentOptions.cs
    (DigestSize, AlertThreshold, AlertQuietHours, OpenAIModel, RetentionDays тощо).
    Якщо параметр не задано, діє значення за замовчуванням із коду. Імена мають збігатися з AgentOptions:
    помилка в імені не дає помилки, параметр просто ігнорується.
  EOT
  type        = map(string)
  default     = {}
}
