# Run Terraform from a manual workflow

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

Infrastructure should be created or updated reproducibly without relying on one laptop, but a change to it is rare and can be destructive.
Code deploys are frequent and low risk. Recorded retroactively; see `.github/workflows/infra.yml` and `.github/workflows/deploy.yml`.

## Decision

Infrastructure runs from a separate workflow, `infra`, started by hand. It always runs `terraform plan`, and applies only when the `apply` checkbox is set.
Code deploys run from a different workflow on pushes to `main`. State lives in Azure Storage, and the pipeline signs in with OIDC.
The pipeline identity (App Registration with a federated credential) and the state storage are created manually once, because Terraform cannot create what it needs in order to run.

## Options considered

- **Apply automatically on push to `main`**: convenient, but a mistaken change could modify or destroy resources without a review of the plan.
- **Terraform only from a laptop with local state**: no extra setup, but not reproducible and the state is easy to lose.
- **Manual workflow with plan then apply**: one extra click, with the plan visible in the run summary before anything changes.

## Consequences

The one-time bootstrap (state storage, identity, three role assignments) is documented in the README.
The identity needs Contributor and Role Based Access Control Administrator at subscription scope, which is broad; narrowing it to a resource group is a possible follow-up.
Deploying code and changing infrastructure stay independent, so an infra change that code depends on must be applied first.
