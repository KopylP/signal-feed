# tests/PostAgent.Tests

xUnit. Папки дзеркалять фічі з `src/PostAgent.Functions`. Запуск: `dotnet test -c Release`.

## Правила

- Без mock-бібліотек: залежності замінюють ручні підробки в `Fakes/` (`InMemoryPostRepository`, `RecordingNotifier`, `FakeChatClient` тощо). Нова залежність це нова підробка там.
- Час через `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) і спільні дані з `TestData`, без `DateTime.Now`.
- Ім'я тесту описує поведінку реченням у `Snake_case` (`Sends_the_no_posts_message_when_there_is_nothing_to_send`). Один `[Fact]` перевіряє одну поведінку; `[Theory]` для набору входів.
- Тестуй чисту логіку напряму, а функції через їхній `Run` із підробками, без мережі й Azure.
- `CompositionTests` перевіряє, що застосунок збирається з валідних налаштувань. Новий обов'язковий параметр додай у `ValidSettings`, інакше цей тест упаде.
- Помилку виправляй із тестом, який без виправлення падає.
