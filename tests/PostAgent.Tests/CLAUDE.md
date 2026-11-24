# tests/PostAgent.Tests

xUnit. Folders mirror the features in `src/PostAgent.Functions`. Run: `dotnet test -c Release`.

## Rules

- No mocking libraries: dependencies are replaced by hand-written fakes in `Fakes/` (`InMemoryPostRepository`, `RecordingNotifier`, `FakeChatClient`, etc.). A new dependency means a new fake there.
- Time comes from `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) and shared data from `TestData`, never from `DateTime.Now`.
- A test name describes the behavior as a sentence in `Snake_case` (`Sends_the_no_posts_message_when_there_is_nothing_to_send`). One `[Fact]` checks one behavior; use `[Theory]` for a set of inputs.
- Test pure logic directly, and test functions through their `Run` method with fakes, with no network or Azure.
- `CompositionTests` checks that the app builds from valid settings. Add a new required parameter to `ValidSettings`, otherwise that test fails.
- Fix a bug together with a test that fails without the fix.
