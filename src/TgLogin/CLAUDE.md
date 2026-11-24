# src/TgLogin

One-off console tool: interactive Telegram sign-in and upload of the session to Blob Storage (usage is in `README.md`).

- It uses `SessionBlob` from `PostAgent.Functions`, so the format and container name match what the function reads. Do not duplicate them here.
- In Azure it authenticates with `DefaultAzureCredential`; for Azurite it takes a connection string. Do not use storage keys.
- Account data (api id/hash, phone) comes from environment variables or is asked for in the console. Never store it in files, and never print the session or login codes.
