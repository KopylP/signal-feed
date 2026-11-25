# Encrypt the Telegram session with a Key Vault key

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

The Telegram session blob (`tg-session/wtelegram.session`) is the reader account: whoever holds it reads Telegram as that account.
Since ADR-0016 the same container also holds `login-attempt.json`, the sign-in in progress (phone, login code, SRP proof of the 2FA password).
Both were readable by anyone with blob read access: the function, the deployer, and anyone with a storage account key or SAS (shared keys are not disabled).
Azure Storage encrypts at rest, but it decrypts for every reader, so it does not narrow who can read the blobs.

## Decision

We encrypt both blobs in code with envelope encryption. Each blob is AES-256-GCM with a data key; the data key is wrapped with an RSA-3072 key
(`tg-session`) in the existing Key Vault (RSA-OAEP-256), and the blob carries the wrapped key and the key version (`Shared/TelegramSession/SessionEnvelope`).
The header and the blob name are authenticated. A process wraps one data key and reuses it with fresh nonces; a key read from a blob is unwrapped once and cached.
Terraform creates the key (Key Vault generates it, so no secret reaches the state) and grants "Key Vault Crypto Service Encryption User" on that key only
to the function and the deployer (`TgLogin`). The app setting `SessionKeyId` names the versionless key; the app refuses to start in Azure without it.
A session stored before this change is read once and written back encrypted.

## Options considered

- **Customer-managed key for the storage account**: Terraform only, but Azure decrypts for every reader, so storage access alone still reads the session.
- **Encrypt with a secret (a symmetric key) in Key Vault**: simple, but the key itself would be readable by the function and handed around; a Key Vault key never leaves the vault.
- **Wrap every write in Key Vault**: no key in memory between writes, but a call to Key Vault on every session flush and every poll of the sign-in.
- **Envelope encryption with a cached data key (chosen)**: storage access alone shows ciphertext, Key Vault is called about once per process, and the key can be rotated.

## Consequences

Reading the session now needs both blob access and the key role, and Key Vault audit logs show who unwrapped. The function and the deployer keep both, so this
protects against storage-only access (keys, SAS, a leaked connection string, a backup), not against a compromised function. A blob names its key version, and only
versions of `SessionKeyId` are accepted, so a blob cannot make the app send its token to another vault. Deleting or disabling the key makes the session unreadable:
sign in again. Rotating the key needs no migration: new writes use the new version. Locally against Azurite (no `SessionKeyId`) the blobs stay in clear.
`TgLogin` needs `SESSION_KEY_ID`. Whoever runs Terraform needs "Key Vault Crypto Officer" on the vault, which Terraform grants; the first apply may need a rerun
while that role propagates.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
