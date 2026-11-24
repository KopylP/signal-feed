# infra: Terraform (Azure)

Імена ресурсів і змінні описані в `README.md` (розділ розгортання). Запуск: локально (`terraform apply`) або вручну через workflow `infra`.

## Правила

- Секретні значення (OpenAI, Telegram) Terraform не створює й у state не кладе: лише посилання Key Vault references у App Settings.
- Доступ між ресурсами через managed identity і RBAC (`roles.tf`), без ключів і рядків підключення.
- Імена: `<скорочення>-${var.name_prefix}` (`rg-`, `plan-`, `log-`, `appi-`, `budget-`). Глобально унікальні (`st`, `kv-`, `func-`) отримують `local.suffix`.
- Нова змінна: `description` українською, тип, `default` для несекретного й однакового для всіх. Особисті дані (телефон, email, chat id) без `default`.
- Новий параметр агента не додавай окремою змінною: його значення за замовчуванням живе в `AgentOptions.cs`, а задається через `app_settings`.
  Окремі змінні лише для обов'язкових, інфраструктурних значень і розкладів (їх читають атрибути таймерів).
- Додаєш змінну: заведи її в `variables.tf`, `terraform.tfvars.example`, у `function.tf` (якщо це App Setting), у `.github/workflows/infra.yml`
  (`TF_VAR_*` з GitHub Variables) і в таблиці README. Якщо змінна має `default`, у workflow передавай її лише коли задана, бо порожня `TF_VAR_*` перекриє `default`.
- Перед завершенням: `terraform fmt` і `terraform validate`.
- Не додавай ресурси, що коштують постійно (Premium plan, VNet, private endpoint тощо), без явного запиту: бюджет $5 на місяць.
