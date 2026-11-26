# src/TgLogin

Console tool: interactive Telegram sign-in and upload of the session to Blob Storage (usage is in `docs/user-guide/installation.md`).
It is for the first installation and for sign-ins the bot cannot do; later renewals go through `/login` in the bot (`src/PostAgent.Core/Login` and `src/PostAgent.Functions/Login`, ADR-0016), which writes the same blob.

- It compiles `StorageNames.cs` from `PostAgent.Functions` (linked in `TgLogin.csproj`), so the blob name and default container match what the function reads. Do not duplicate them here.
- In Azure it authenticates with `DefaultAzureCredential`; for Azurite it takes a connection string. Do not use storage keys.
- In Azure it encrypts the session with the Key Vault key from `SESSION_KEY_ID` (linked `SessionEnvelope` and `KeyVaultKeyWrapper`, ADR-0017) and refuses to upload it in clear.
- Account data (api id/hash, phone) comes from environment variables or is asked for in the console. Never store it in files, and never print the session or login codes.
