# src/PostAgent.Functions: Azure Functions

Загальні правила C# у кореневому `CLAUDE.md`. Структура фіч: `README.md`.

## Фічі й реєстрація

- Фіча = папка з функцією, логікою й інтерфейсами; адаптери (Telegram, Azure, модель) у підпапках `Telegram/`, `Storage/`, `Ai/`.
- Кожна фіча має `XxxServices.AddXxx(this IServiceCollection)`, який викликається з `ServiceCollectionExtensions.AddPostAgent`.
- Функція тонка: бере залежності через конструктор, викликає логіку й ловить лише те, що вміє обробити. Чиста логіка (відбір, форматування) у статичних класах і тестується окремо.
- Нове очищення це клас `ICleanupTask` у `Cleanup/`, зареєстрований у `CleanupServices`.

## Налаштування

- Усі App Settings читаються через `AgentOptions` (`Shared/Settings`). Значення за замовчуванням тільки там, це єдине джерело.
- Діапазони й обов'язковість: DataAnnotations; складніші перевірки в `AgentOptionsValidator`. Налаштування перевіряються на старті (`ValidateOnStart`).
- Розклади не входять в `AgentOptions`: їх читають атрибути `[TimerTrigger("%Назва%")]` напряму.
- Додаєш чи змінюєш параметр: онови таблицю в README й `local.settings.json.example`; для Azure див. `infra/CLAUDE.md`.
- Секрети (`OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash`) приходять з Key Vault. Не логуй їх; токен бота редагує `TelegramTokenRedactor`.

## Дані й черги

- Таблиця `Posts` розбита на дні (`PartitionKey` = дата). Зміна схеми `PostEntity` це зміна сумісності з уже збереженими даними: роби нові поля необов'язковими.
- Повідомлення черги `to-score` обробляється ідемпотентно: повторна доставка не має давати дублів.
- Виклики моделі йдуть через `IChatClient` і строгі схеми з C#-типів (`ChatClientExtensions.AskAsync`). Текст постів це дані, а не інструкції.

## Не забудь

- Новий NuGet-пакет: версія в `Directory.Packages.props`, пакет у `.csproj` без версії.
- Зміна поведінки функції: тест у `tests/PostAgent.Tests/` у відповідній папці.
