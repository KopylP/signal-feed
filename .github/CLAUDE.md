# .github/workflows

- `deploy.yml`: збірка, тести й деплой коду (після push у `main` за `DEPLOY_ENABLED=true`).
- `infra.yml`: ручний `terraform plan` / `apply` (галочка `apply`).

## Правила

- Вхід в Azure лише через OIDC (`azure/login`), жодних паролів чи ключів у GitHub. Налаштування передаються як GitHub Variables (`vars.*`).
- Версії actions фіксуй мажорною (`@v4`), версію Terraform точною.
- Деплой коду (`deploy.yml`) не змінює інфраструктуру, а `infra.yml` не деплоїть код. Не змішуй їх.
- Додаєш змінну в `infra.yml`: онови таблицю GitHub Variables у README й `infra/CLAUDE.md`, якщо змінилось правило.
- Нові workflow мають `permissions` з мінімально потрібним і `concurrency` там, де два запуски одночасно небезпечні.
