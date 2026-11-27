# Signal Feed

An agent that reads Telegram channels and groups for you and sends only the posts worth your attention.

## Goal

Telegram has more channels and groups than anyone can read. Signal Feed turns that flow into a short daily digest and rare instant alerts,
chosen by an AI model against **your own rules**: your topics and your examples of a good post.

## Who it is for

| Reader | What they get | Start with |
|---|---|---|
| **Operator**: a person who wants a personal feed for a set of channels and groups | Their own copy running in their own Azure subscription; nothing is shared with anyone | [User Guide](docs/user-guide/overview.md) |
| **Developer**: a person who changes or extends the agent | The architecture, the code structure, the pipelines and the reasons behind decisions | [Developer Guide](docs/developer-guide/architecture.md) |

## What it does

```
Telegram ──► Collector (hourly) ──► Embedding (drops duplicates) ──► Scorer (OpenAI) ──┬──► Daily digest ──► You
                                                                                        └──► Instant alert (groups, followed topics) ──► You
```

1. **Collects** new posts from the public channels and groups you choose, every hour.
2. **Scores** each post with OpenAI against your rules; the same news from another channel is recognized by its meaning first and is not scored again.
3. **Delivers** a digest of the best posts once a day, in sections (main news, what matters for your work, what to read, what to skip), and an instant alert when a discussion in a chosen group bursts.

Runs on Azure Functions (.NET 10), with state in Azure Storage and secrets in Key Vault. The Telegram messages and the digest text written by the model are in Ukrainian; the prompts and everything else are in English.

## Where to start

| You are... | Start here |
|---|---|
| New to the project | [Overview](docs/user-guide/overview.md) and [How it works](docs/user-guide/how-it-works.md) |
| Setting up your own copy | [Installation](docs/user-guide/installation.md), then [Configuration](docs/user-guide/configuration.md) |
| Something does not work | [Troubleshooting](docs/user-guide/troubleshooting.md) |
| A developer | [Architecture](docs/developer-guide/architecture.md), [Code structure](docs/developer-guide/code-structure.md), [Local development](docs/developer-guide/local-development.md) |
| Working on pipelines and releases | [CI/CD](docs/developer-guide/ci-cd.md) |

The full map is in [`docs/README.md`](docs/README.md). Decisions and their reasons are in [`docs/adr/`](docs/adr/README.md).
