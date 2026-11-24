resource "azurerm_consumption_budget_resource_group" "main" {
  name              = "budget-${var.name_prefix}"
  resource_group_id = azurerm_resource_group.main.id
  amount            = var.monthly_budget_usd
  time_grain        = "Monthly"

  time_period {
    start_date = formatdate("YYYY-MM-01'T'00:00:00'Z'", timestamp())
  }

  dynamic "notification" {
    for_each = [80, 100]
    content {
      enabled        = true
      threshold      = notification.value
      operator       = "GreaterThanOrEqualTo"
      contact_emails = [var.budget_alert_email]
    }
  }

  lifecycle {
    ignore_changes = [time_period]
  }
}
