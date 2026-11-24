# tools

File-based apps run with `dotnet run tools/<name>.cs` (no project file, .NET 10). Usage is in the header comment of each file.

- `run-now.cs` starts a job through the `run-now` queue. The queue and job names are copied from `StorageNames.RunNowQueue` and the `IJob` names (a test in `CompositionTests` fails if they drift).
- Packages are referenced with `#:package` and no version: versions live in `Directory.Packages.props`.
- Authenticate with `DefaultAzureCredential` or a connection string for Azurite. Never use storage keys, and never print secrets.
- `dotnet build -c Release` does not compile these files. After editing one, run `dotnet build tools/<name>.cs -c Release`.
