# Signal Feed

Агент відбору постів із Telegram на Azure Functions. Щогодини читає публічні канали й групи,
оцінює пости через OpenAI за вашими правилами, раз на день надсилає дайджест найкращих,
а для вибраних груп ще й сповіщає одразу, коли в обговоренні щось відбувається.

## Як це працює

```
Collector (щогодини) ──► Posts (Table) ──► to-score (Queue) ──► Scorer (OpenAI)
                                                                     │
                       AlertFunction (щогодини, лише групи) ◄───────────┤
                       Digest (раз на день) ◄────────────────────────┘
```

| Функція | Тригер | Що робить |
|---|---|---|
| `CollectorFunction` | `%CollectorSchedule%` | Читає нові повідомлення джерел через MTProto, зберігає пости, ставить їх у чергу пачками по `ScoringBatchSize` |
| `ScorerFunction` | черга `to-score` | Оцінює пости через OpenAI (Structured Outputs), пише Score, Topic, Reason, DuplicateOf |
| `AlertFunction` | `%AlertSchedule%` | Надсилає алерт про сплеск або дуже важливе повідомлення в групах зі списку `AlertSources` |
| `DigestFunction` | `%DigestSchedule%` | Вибирає найкращі пости, просить модель написати підсумок і надсилає дайджест (завжди) |
| `CleanupFunction` | `%CleanupSchedule%` | Виконує всі `ICleanupTask`. Зараз це видалення з `Posts` днів старших за `RetentionDays` |

**Канали й групи.** Канали: кожен пост оцінюється окремо. Групи: якщо за запуск прийшло не більше
`MaxItemsPerGroup` (5) повідомлень, вони теж окремі пости. Якщо більше, повідомлення за часом ділиться на
`MaxItemsPerGroup` пачок приблизно рівного розміру, і кожну пачку оцінюють одним викликом. Чим швидший потік,
тим більша пачка, а вартість оцінювання не росте. У тексті пачки першими йдуть повідомлення з найбільшою кількістю
реакцій, пересилань і відповідей, тому ключове не губиться. Мінімальна довжина `MinPostChars` застосовується до окремих
повідомлень, а в пачці короткі репліки лишаються: саме їхня кількість показує сплеск.

**Алерти.** Лише для груп зі списку `AlertSources`, канали алертів не викликають. Алерт приходить, якщо оцінка
повідомлення чи пачки ≥ `AlertThreshold` (9) або в пачці ≥ `BurstMinPosts` (8) повідомлень. Про одну групу
сповіщаємо не частіше ніж раз на `AlertCooldownHours`, за один запуск не більше `AlertMaxPerRun` записів.
У `AlertQuietHours` (наприклад, `23:00-07:00`) алерти не надсилаються, такі пости потрапляють у дайджест.
Пост, про який вже сповістили, у дайджесті не повторюється.

**Підсумок у дайджесті.** Після вибору постів `DigestSummarizer` робить один додатковий виклик OpenAI за промптом
[`prompts/digest.md`](prompts/digest.md). Модель повертає короткий огляд дня (що головне й які теми повторюються) і для кожного поста
пояснення, що саме в ньому цікаве. Посилання модель не пише: їх додає система з даних постів, тож вигадати їх вона не може.
Якщо OpenAI недоступний або відповідь некоректна, дайджест усе одно приходить без підсумку, з короткими причинами оцінки.

**Очищення даних.** Таблиця `Posts` розбита на дні, тому `CleanupFunction` видаляє цілі дні старші за `RetentionDays`.
`ChannelState` (курсори читання) не чіпається. Щоб очищати щось нове (іншу таблицю, чергу чи blob), додайте клас, що реалізує
`ICleanupTask`, і зареєструйте його в `Program.cs`: функція виконає його разом з іншими.
Резервне вікно дайджесту в 48 годин працює лише в межах даних, що лишилися, тобто з `RetentionDays = 1` це вчорашній і сьогоднішній день.

## Налаштування (App Settings)

| Параметр | За замовчуванням | Опис |
|---|---|---|
| `Sources` | | username каналів і груп через кому |
| `CollectorSchedule` | `0 0 * * * *` | CRON збору |
| `DigestSchedule` | `0 0 8 * * *` | CRON дайджесту |
| `AlertSchedule` | `0 30 * * * *` | CRON перевірки алертів |
| `DigestSize` | 5 | скільки постів у дайджесті (не більше 8, інакше повідомлення не вміститься в ліміт Telegram) |
| `InterestingThreshold` | 7 | від якої оцінки пост «🔥 Цікаве» |
| `ScoringBatchSize` | 10 | постів в одному повідомленні черги |
| `MaxPostChars` / `MinPostChars` | 1500 / 80 | обрізання й мінімальна довжина |
| `MaxItemsPerGroup` | 5 | постів на групу за запуск |
| `MaxBundleChars` | 3000 | розмір тексту пачки |
| `AlertSources` | | групи, які можуть викликати алерт |
| `AlertThreshold` / `BurstMinPosts` | 9 / 8 | пороги алерту |
| `AlertWindowHours` / `AlertCooldownHours` | 3 / 3 | вікно оцінених постів і пауза між алертами однієї групи |
| `AlertMaxPerRun` | 3 | записів в одному алерті (не більше 8) |
| `AlertQuietHours` / `AlertTimeZone` | порожньо / `Europe/Kyiv` | тихі години |
| `OpenAIModel` | `gpt-5.4-nano` | модель оцінювання |
| `OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash` | | **секрети**, лише в Key Vault |
| `TelegramChatId`, `TelegramApiId`, `TelegramPhone` | | куди слати; дані акаунта для читання |
| `SessionBlobContainer` | `tg-session` | контейнер із сесією Telegram |
| `CleanupSchedule` | `0 0 3 * * *` | CRON очищення (UTC) |
| `RetentionDays` | 1 | скільки днів назад зберігати пости: 1 означає вчорашню й сьогоднішню партиції |
| `FirstRunLookbackHours` | 24 | скільки годин історії читати при першому запуску для нового джерела |

**Де значення за замовчуванням.** Єдине джерело: `AgentOptions.cs`. У Terraform окремими змінними є лише обов'язкові й
інфраструктурні значення (джерела, дані Telegram, розклади, контейнер сесії). Усе інше задається через змінну `app_settings`
з іменами з таблиці вище, наприклад `app_settings = { DigestSize = "6", AlertQuietHours = "23:00-07:00" }`.
Якщо параметр не задано, діє значення з коду. Помилка в імені ключа не дає помилки, параметр просто ігнорується.

Налаштування перевіряються під час старту: обов'язкові значення (джерела, ключ OpenAI, токен бота, дані Telegram), діапазони
(наприклад, `DigestSize` і `AlertMaxPerRun` від 1 до 8, `ScoringBatchSize` 1–100, `MaxItemsPerGroup` 1–50, `RetentionDays` 1–365),
формат `AlertQuietHours` із часовим поясом, а також що `AlertWindowHours + AlertCooldownHours` не більше за `RetentionDays * 24`.
Якщо щось не так, застосунок не запуститься, а в логу буде назва параметра.

### Як змінити розклад, джерела й промпт

**Розклад.** CRON має шість полів (`секунди хвилини години день місяць день-тижня`) і задається **в UTC**.
`0 0 8 * * *` це 08:00 UTC, тобто 10:00 за Києвом взимку (UTC+2) і 11:00 влітку (UTC+3), тож літній і зимовий час доведеться враховувати самостійно.
Розклади читаються через `%CollectorSchedule%` та інші, тому змінити їх можна без перезбірки коду. Достатньо оновити
App Setting (або змінну Terraform і `terraform apply`), і застосунок перезапуститься з новим розкладом:

```bash
az functionapp config appsettings set -g <rg> -n <function-app> \
  --settings DigestSchedule="0 0 6 * * *" CollectorSchedule="0 0 */2 * * *"
```

**Джерела.** `Sources` (і `AlertSources`) це username через кому, `@` і `t.me/` можна не прибирати:

```bash
az functionapp config appsettings set -g <rg> -n <function-app> \
  --settings Sources="channel_one,channel_two,some_group" AlertSources="some_group"
```

**Промпти.** Файли [`prompts/scoring.md`](prompts/scoring.md) (оцінювання) і [`prompts/digest.md`](prompts/digest.md) (підсумок дайджесту) копіюються у збірку, тому після зміни
потрібен новий деплой (достатньо push у `main`). У `scoring.md` заповніть свої теми замість `{тема 1}`
і приклади замість `{реальний пост...}`.

## Локальний запуск з Azurite

Потрібні: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local),
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (`npm i -g azurite`).

```bash
azurite --silent --location .azurite &
cp src/PostAgent.Functions/local.settings.json.example src/PostAgent.Functions/local.settings.json
# заповніть заглушки: ключ OpenAI, токен бота, дані Telegram

# одноразовий вхід у Telegram (сесія піде в Azurite)
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...
dotnet run --project src/TgLogin -- "UseDevelopmentStorage=true"

cd src/PostAgent.Functions && func start
```

Локально таблиці, черга й контейнер створюються автоматично. Щоб одразу побачити результат, запустіть таймер вручну:
`curl -X POST http://localhost:7071/admin/functions/CollectorFunction -H "Content-Type: application/json" -d "{}"`.

Тести: `dotnet test`.

## Розгортання

Потрібні Azure CLI (`az login`), Terraform ≥ 1.5, підписка Azure.

**Перед стартом.** Перевірте, що Flex Consumption доступний у вашому регіоні:
`az functionapp list-flexconsumption-locations -o table`, і за потреби змініть `location`.

### 1. terraform apply

```bash
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
cd infra
cp terraform.tfvars.example terraform.tfvars   # заповніть, зокрема deployer_object_id і budget_alert_email
terraform init
terraform apply
```

`deployer_object_id`: `az ad signed-in-user show --query id -o tsv`. State за замовчуванням локальний,
для віддаленого дивіться `backend.tf.example`.

Terraform створює: resource group, storage (контейнери `deployment-package` і `tg-session`, таблиці `ChannelState` і `Posts`,
чергу `to-score`), Key Vault (RBAC), Log Analytics (30 днів, 0.1 ГБ на добу), Application Insights, план FC1 і function app
із managed identity та ролями, бюджет $5 на місяць (сповіщення при 80% і 100%).

### 2. Секрети в Key Vault

Значення секретів Terraform не створює, щоб вони не потрапили в state:

```bash
KV=$(terraform output -raw key_vault_name)
read -rsp "OpenAI API key: " V && az keyvault secret set --vault-name $KV --name OpenAIApiKey --value "$V" >/dev/null; echo
read -rsp "Telegram bot token: " V && az keyvault secret set --vault-name $KV --name TelegramBotToken --value "$V" >/dev/null; echo
read -rsp "Telegram api_hash: " V && az keyvault secret set --vault-name $KV --name TelegramApiHash --value "$V" >/dev/null; echo
```

Застосунок читає їх через Key Vault references. Якщо посилання не підхопилося, перевірте роль `Key Vault Secrets User`
у identity функції й перезапустіть застосунок (`az functionapp restart`). Нерозв'язане посилання застосунок помітить сам:
під час старту буде помилка з назвою секрету.

### 3. TgLogin (вхід у Telegram)

Використовуйте окремий акаунт. `api_id` та `api_hash` беруться на [my.telegram.org](https://my.telegram.org). Якщо групу не вдається
прочитати, вступіть у неї цим акаунтом.

```bash
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...
dotnet run --project src/TgLogin -- $(terraform -chdir=infra output -raw storage_account_name)
```

Програма запитає код із Telegram і пароль 2FA, а сесію завантажить у контейнер `tg-session` через `DefaultAzureCredential`
(ваш `az login`, роль Storage Blob Data Contributor уже видана Terraform). Якщо сесія застаріє, повторіть цей крок.

### 4. Деплой коду

```bash
dotnet publish src/PostAgent.Functions -c Release -o publish
cd publish
func azure functionapp publish $(terraform -chdir=../infra output -raw function_app_name)
```

## GitHub Actions: build, test, deploy через OIDC

Workflow: [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml). Збирає й тестує на push у `main` і на кожен PR, а з `main` ще й деплоїть (деплої не виконуються одночасно). Запускається й вручну (`workflow_dispatch`).

Одноразове налаштування OIDC (без паролів):

```bash
APP_ID=$(az ad app create --display-name signal-feed-deploy --query appId -o tsv)
az ad sp create --id $APP_ID
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-owner>/<repo>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
az role assignment create --assignee $APP_ID --role Contributor \
  --scope $(az functionapp show -g <rg> -n <function-app> --query id -o tsv)
```

У репозиторії GitHub (Settings → Secrets and variables → Actions → Variables) задайте:
`AZURE_CLIENT_ID` (це `$APP_ID`), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_FUNCTIONAPP_NAME`.

**Деплой вимкнений за замовчуванням.** Без змінної `DEPLOY_ENABLED` зі значенням `true` workflow лише збирає й тестує, а крок деплою
пропускається. Коли будете готові розгортати, задайте цю змінну там же, де й решту.

## Безпека й вартість

- У коді й репозиторії немає секретів. `local.settings.json` у `.gitignore`, у `local.settings.json.example` лише заглушки.
- Доступ до storage через managed identity. Токен бота не потрапляє в логи й телеметрію: HTTP-логування клієнта вимкнено, а
  `TelegramTokenRedactor` замінює токен у записах залежностей Application Insights.
- До OpenAI йдуть тексти постів і, для груп, імена авторів та username учасників. Це персональні дані третіх осіб, хоча джерела публічні.
- Орієнтовно: функції в межах безкоштовної квоти Flex Consumption, storage копійки, основна стаття витрат OpenAI.
  Бюджет $5 на resource group дає сповіщення на email при 80% і 100%.

## Готові рішення замість власного коду

| Задача | Що використано |
|---|---|
| Виклики моделі, відповідь за строгою схемою | `Microsoft.Extensions.AI` (`IChatClient`) з адаптером OpenAI; схема будується з C#-типів, `strict: true` вмикає `ChatClientExtensions.AskAsync` |
| Bot API Telegram | `Telegram.Bot` |
| Читання каналів і груп (MTProto) | `WTelegramClient` |
| Table, Queue, Blob, автентифікація | `Azure.Data.Tables`, `Azure.Storage.Queues`, `Azure.Storage.Blobs`, `Azure.Identity` |
| Повтори й таймаути HTTP | `Microsoft.Extensions.Http.Resilience` |
| Перевірка налаштувань | `Options` з DataAnnotations і `ValidateOnStart` |

Щоб замінити модель чи провайдера, достатньо інакше зареєструвати `IChatClient` у `Program.cs`: код оцінювання й підсумку про провайдера не знає.

## Структура

Код організований по фічах: кожна папка містить усе для однієї можливості (функцію, логіку, інтерфейси, реєстрацію сервісів).
Технічні деталі фічі (Telegram, Azure, модель) лежать у її підпапках, а спільне для кількох фіч у `Shared/`. Namespace збігається
з шляхом папки, а залежності між фічами видно з `using`.

```
src/PostAgent.Functions/
├── Collecting/            збір постів з Telegram
│   ├── Telegram/          читач каналів і груп (WTelegramClient), сесія в Blob
│   └── Storage/           курсори читання джерел
├── Scoring/               оцінювання постів
│   ├── Ai/                виклик моделі
│   └── Storage/           черга to-score
├── Digest/                денний дайджест
│   └── Ai/                підсумок від ШІ
├── Alerts/                алерти про сплески в групах
├── Cleanup/               очищення старих даних
├── Shared/                спільне для фіч
│   ├── Posts/             модель посту, IPostRepository (Storage/: таблиця Posts)
│   ├── Notifications/     відправка в Telegram (Telegram/: Telegram.Bot)
│   ├── Ai/                IChatClient, промпти
│   ├── Settings/          AgentOptions і валідатор
│   ├── Infrastructure/    клієнти Table, Queue, Blob
│   └── Common/            дрібні розширення
├── Program.cs
└── ServiceCollectionExtensions.cs   складання застосунку з усіх фіч
src/TgLogin/               одноразовий вхід у Telegram
tests/PostAgent.Tests/     xUnit, ті самі папки; Fakes/ — підроблені залежності для тестів функцій
Directory.Packages.props   версії всіх NuGet-пакетів в одному місці
infra/                     Terraform: storage, keyvault, function, monitoring, roles, budget
                           (main.tf: resource group й імена; variables, outputs, providers)
prompts/                   scoring.md (оцінювання) і digest.md (підсумок дайджесту)
.github/workflows/         build, test, deploy
```

**Нова фіча.** Створіть папку з функцією, логікою й інтерфейсами, адаптери покладіть у її підпапки, додайте `AddXxx()` для її
сервісів і викличте його в `ServiceCollectionExtensions.AddPostAgent`. Нове очищення це один клас `ICleanupTask` у `Cleanup/`.

## Відомі обмеження

- **Повтори повідомлень.** Якщо Telegram відповів на `sendMessage` із затримкою, повтор може дати дубль дайджесту чи алерта. Це свідомий вибір на користь того, щоб не втратити повідомлення.
- **Збій збору.** Повідомлення «Збір не вдався» приходить щогодини, поки причину не усунуто.
- **Пачки після збою.** Якщо запуск Collector обірвався між записом постів і оновленням курсору, а за цей час прийшли нові повідомлення, ті самі повідомлення групи можуть потрапити у дві різні пачки.
- **Перший запуск.** Для зайнятої групи читання історії за добу це багато запитів поспіль, тому можливий FLOOD_WAIT. Джерело тоді пропускається до наступного запуску.
- **Свіжі повідомлення** ще не набрали реакцій, тому порядок у пачці за помітністю спершу приблизний.
- **Позначка «надіслано».** Якщо повідомлення пішло, а запис статусу в таблицю не вдався, наступний запуск може повторити алерт.
- **Тести сховища** не входять до `dotnet test`: `PostRepository` перевіряється вручну на Azurite.
