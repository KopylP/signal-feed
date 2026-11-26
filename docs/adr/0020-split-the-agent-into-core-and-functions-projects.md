# Split the agent into a Core library and a Functions host

- Status: Accepted
- Date: 2026-11-26
- Deciders: Maintainer

## Context

Everything lived in one project, `PostAgent.Functions`: the Azure Functions entry points, but also the chat handlers, the feedback buttons, the statistics, the stores and the adapters.
Some features have no function at all (the 👍/👎 buttons, `/stats`), so the name promised a host and the project held an application. The rule that logic does not depend on the host
(and that `Shared/` does not depend on a feature) held only by an architecture test over source text. The Azure Functions isolated worker finds `[Function]` methods only in the assembly it runs.

## Decision

Two projects. `PostAgent.Core` is a class library with the features and `Shared/` and no Azure Functions package. `PostAgent.Functions` is the host: `Program`, `host.json` and the `[Function]` classes,
thin, each in the folder of its feature. Namespaces do not change (`PostAgent.Chat`, `PostAgent.Shared...`). `TgLogin` links its few files from Core. An architecture test fails when a `[Function]` class
is in Core or when Core references the Functions host.

## Options considered

- **One project (as before)**: no churn, but the name lies and the host dependency is not enforced by the compiler.
- **A project per feature**: strongest isolation, but ten small projects for features that all depend only on `Shared`, and the functions still have to stay in the host assembly.
- **Core plus Functions (chosen)**: an honest name, the host is the only place that knows Azure Functions, logic is tested without it, and the move kept every namespace.

## Consequences

A feature with a trigger now has two files in two projects (the logic in Core, the thin function in Functions); the folder names match. Features may still use each other only through `Shared`; that
is enforced by the test, not by the compiler. Revisit if a feature needs to be built and deployed on its own.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
