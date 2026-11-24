# ---------- Інфраструктура ----------

variable "location" {
  description = "Регіон Azure. Перевірте, що Flex Consumption доступний: az functionapp list-flexconsumption-locations"
  type        = string
  default     = "westeurope"
}

variable "name_prefix" {
  description = "Префікс імен ресурсів (малі літери, цифри, дефіси)."
  type        = string
  default     = "signalfeed"
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

# ---------- App Settings: джерела й розклади (CRON у UTC) ----------

variable "sources" {
  description = "Username каналів і груп через кому."
  type        = string
  default     = ""
}

variable "collector_schedule" {
  type    = string
  default = "0 0 * * * *"
}

variable "digest_schedule" {
  type    = string
  default = "0 0 8 * * *"
}

variable "alert_schedule" {
  type    = string
  default = "0 30 * * * *"
}

# ---------- App Settings: дайджест, збір, оцінювання ----------

variable "digest_size" {
  type    = number
  default = 5
}

variable "interesting_threshold" {
  type    = number
  default = 7
}

variable "scoring_batch_size" {
  type    = number
  default = 10
}

variable "max_post_chars" {
  type    = number
  default = 1500
}

variable "min_post_chars" {
  type    = number
  default = 80
}

variable "retention_days" {
  type    = number
  default = 14
}

# ---------- App Settings: групи й алерти ----------

variable "max_items_per_group" {
  description = "Не більше стільки постів на групу за запуск, решта стискається в пачки."
  type        = number
  default     = 5
}

variable "max_bundle_chars" {
  type    = number
  default = 3000
}

variable "alert_sources" {
  description = "Групи, які можуть викликати алерт (username через кому). Канали алертів не викликають."
  type        = string
  default     = ""
}

variable "alert_window_hours" {
  type    = number
  default = 3
}

variable "alert_cooldown_hours" {
  type    = number
  default = 3
}

variable "alert_threshold" {
  type    = number
  default = 9
}

variable "burst_min_posts" {
  type    = number
  default = 8
}

variable "alert_max_per_run" {
  type    = number
  default = 3
}

variable "alert_quiet_hours" {
  description = "Тихі години алертів, наприклад 23:00-07:00. Порожньо — вимкнено."
  type        = string
  default     = ""
}

variable "alert_time_zone" {
  type    = string
  default = "Europe/Kyiv"
}

# ---------- App Settings: OpenAI і Telegram ----------

variable "openai_model" {
  type    = string
  default = "gpt-5.4-nano"
}

variable "telegram_chat_id" {
  description = "Куди слати дайджест і алерти."
  type        = string
  default     = ""
}

variable "telegram_api_id" {
  type    = string
  default = ""
}

variable "telegram_phone" {
  type    = string
  default = ""
}

variable "session_blob_container" {
  type    = string
  default = "tg-session"
}
