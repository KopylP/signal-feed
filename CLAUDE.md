# Signal Feed

Агент відбору постів із Telegram на Azure Functions (.NET 10, isolated worker). Опис роботи, налаштувань і розгортання: `README.md`.
Правила для окремих частин лежать у `CLAUDE.md` своєї папки: `infra/`, `src/PostAgent.Functions/`, `src/TgLogin/`, `tests/PostAgent.Tests/`, `prompts/`, `.github/`.

## Підтримуй документацію актуальною

Після виконання завдання, якщо щось змінилось, онови в тому ж коміті `README.md` і потрібні `CLAUDE.md`
(налаштування, команди, змінні, структура папок, правила, які ми змінили). Нові правила додавай у `CLAUDE.md` тієї папки, якої вони стосуються.
Не дублюй те, що вже є в README, і тримай кожен `CLAUDE.md` коротким (до ~60 рядків).

## Команди

```bash
dotnet restore && dotnet build -c Release --no-restore   # збірка; попередження вважаються помилками
dotnet test -c Release                                    # усі тести
terraform -chdir=infra fmt -check && terraform -chdir=infra validate   # після init -backend=false
```

Перед завершенням завдання збірка й тести мають проходити.

## Загальні правила коду (C#)

- Мова: ідентифікатори, логи й повідомлення про помилки англійською; коментарі, README, промпти й тексти для користувача українською.
- `Nullable` увімкнено, `TreatWarningsAsErrors = true`. Не гаси попередження через `#pragma` чи `!`, виправляй причину.
- File-scoped namespace, що збігається зі шляхом папки (корінь `PostAgent`). Ім'я файлу збігається з головним типом; невеликі пов'язані типи (enum, record) можуть лежати разом.
- Усі класи `sealed` (або `static`), якщо тільки не задумані для успадкування. DI через primary constructor.
- Код організований по фічах (див. «Структура» в README). Не клади логіку фічі в `Shared/`, а в `Shared/` не додавай залежностей від фіч.
- Метод, що робить I/O, асинхронний, приймає `CancellationToken ct` останнім параметром і передає його далі.
  Ловиш `Exception`: виключай `OperationCanceledException` (`when (ex is not OperationCanceledException)`).
- Час береться з `TimeProvider`, ніколи з `DateTime.Now` чи `DateTimeOffset.UtcNow`.
- Логи: шаблони з іменованими параметрами (`log.LogInformation("Digest sent: {Count} posts", n)`), без інтерполяції рядків.
  Секрети й токени в лог не потрапляють.
- Коментарі пояснюють, чому так, а не що робить код. Не коментуй очевидне.
- Версії NuGet-пакетів лише в `Directory.Packages.props`; у `.csproj` вказуй пакет без версії.

## Не комітити

Секрети, `local.settings.json`, `terraform.tfvars`, `backend.tf`, state. Дані особи (телефон, email, chat id) не додавай у коміт як значення за замовчуванням.
