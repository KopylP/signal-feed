# Signal Feed documentation

Signal Feed reads Telegram channels and groups, picks the posts worth your attention and sends them to you as a daily digest, with instant alerts for hot discussions.

The documentation has two parts, written for two kinds of reader. Pick the one that matches what you want to do.

| I want to... | Read | Audience |
|---|---|---|
| Understand what the agent does and what it costs | [User Guide](user-guide/overview.md) | Anyone who runs the agent for themselves |
| Run my own copy in Azure | [User Guide: installation](user-guide/installation.md) | Operator |
| Tune sources, schedules and thresholds | [User Guide: configuration](user-guide/configuration.md) | Operator |
| Fix a problem | [User Guide: troubleshooting](user-guide/troubleshooting.md) | Operator |
| Understand how the system is built | [Developer Guide: architecture](developer-guide/architecture.md) | Developer |
| Change the code | [Developer Guide: code structure](developer-guide/code-structure.md) | Developer |
| Build and test on my machine | [Developer Guide: local development](developer-guide/local-development.md) | Developer |
| Work with pipelines, releases and rollback | [Developer Guide: CI/CD](developer-guide/ci-cd.md) | Developer |
| Learn why something was decided | [Architecture decision records](adr/README.md) | Developer |

## User Guide

Task-oriented: what the agent does, how to install it, how to configure it, what to do when something is wrong.
No knowledge of the code is needed.

1. [Overview](user-guide/overview.md): what you get, what you need, privacy and cost.
2. [How it works](user-guide/how-it-works.md): channels and groups, alerts, the digest, data retention.
3. [Installation](user-guide/installation.md): from an empty Azure subscription to the first digest.
4. [Configuration](user-guide/configuration.md): every setting, and how to change schedules, sources and prompts.
5. [Troubleshooting](user-guide/troubleshooting.md): symptoms, causes, fixes, and known limitations.
6. [Handover](user-guide/handover.md): move the agent to another person, clear the history, get the first report.

## Developer Guide

Explains how the system is built and how to work on it.

1. [Architecture](developer-guide/architecture.md): components, data flow, Azure resources, identity, design decisions.
2. [Code structure](developer-guide/code-structure.md): folders by feature, libraries, how to add a feature.
3. [Local development](developer-guide/local-development.md): run with Azurite, tests, formatting.
4. [CI/CD](developer-guide/ci-cd.md): GitHub Actions, OIDC, the infra workflow, releases, rollback.

The rules for contributors (and for Claude Code) live in the `CLAUDE.md` files next to the code they cover.
