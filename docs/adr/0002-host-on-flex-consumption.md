# Host on Azure Functions Flex Consumption

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

The agent is a set of timer and queue functions: collect hourly, check alerts hourly, digest once a day, cleanup once a day.
It is idle most of the time, and the monthly budget is $5 (`infra/budget.tf`).
Recorded retroactively; the choice is implemented in `infra/function.tf`.

## Decision

We run the function app on the Flex Consumption plan (`FC1`, Linux, `dotnet-isolated`, 512 MB instances, at most 10 instances).

## Options considered

- **Flex Consumption (FC1)**: pay per execution with a free grant, identity-based storage access, deployment package in Blob. Needs an explicit service plan, one app per plan, and a region where it is available.
- **Classic Consumption (Y1)**: also pay per use, but an older model without the per-instance memory size and the scaling controls of Flex Consumption.
- **Premium or Dedicated plan**: always-on cost, far above the budget for a workload this small.

## Consequences

An explicit `azurerm_service_plan` is required, and the code ships as a zip in the `deployment-package` container.
The region must support Flex Consumption (`az functionapp list-flexconsumption-locations`). Cost stays near zero for the function itself,
so the main cost is the OpenAI usage. Revisit if the schedule becomes much busier or cold starts start to matter.
