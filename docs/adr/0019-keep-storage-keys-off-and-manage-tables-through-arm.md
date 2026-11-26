# Keep the storage account keys off and manage tables through the Resource Manager

- Status: Accepted
- Date: 2026-11-26
- Deciders: Maintainer

## Context

Everything in the app already reaches storage with managed identity and RBAC (ADR-0003, ADR-0017); the account keys were still on, so a leaked key would give full access to every blob, table and queue.
When they were switched off (`shared_access_key_enabled = false`, `storage_use_azuread = true`), the next `infra` plan failed: `azurerm_storage_table` reads the table ACLs with a shared key
whatever `storage_use_azuread` says (`403 Key based authentication is not permitted on this storage account`). Blobs and queues through `azurerm` work with Entra ID; tables do not.

## Decision

The account keys stay off. The three tables are managed with `azapi_resource` (`Microsoft.Storage/storageAccounts/tableServices/tables`) through the Resource Manager, which needs no key,
and the existing tables were adopted with `import` blocks (the old `azurerm_storage_table` resources left the state with `removed` blocks that never destroy). Containers and queues stay on `azurerm`
(data plane, Entra ID); the Terraform identity holds the Storage Blob, Table and Queue Data roles for that (`roles.tf`).

## Options considered

- **Turn the keys back on**: the `azurerm` tables work again, but a leaked key opens everything and the hardening is lost.
- **Keep `azurerm_storage_table` and give Terraform a key from outside**: reintroduces a secret into the pipeline, which ADR-0003 avoids.
- **Create tables in code at startup**: the app would need the table-create right and the infrastructure would no longer describe them.
- **`azapi_resource` through ARM (chosen)**: no key, no secret, the tables stay in Terraform; the cost is one more provider resource type for tables and a one-time import.

## Consequences

`infra` plan and apply work with the keys off. A new table is one more entry in `local.tables` (`infra/storage.tf`), never an `azurerm_storage_table`. The `import` blocks can be removed after the first apply.
Revisit when `azurerm` reads table ACLs through Entra ID: the tables could move back, but nothing requires it.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
