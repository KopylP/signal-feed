# Signal Feed

A Telegram post-selection agent on Azure Functions. Every hour it reads the public channels and groups you choose,
scores the posts with OpenAI against your own rules, sends a digest of the best ones once a day,
and for selected groups also alerts you right away when something is going on in the discussion.

```
Telegram ──► Collector (hourly) ──► Scorer (OpenAI) ──┬──► Daily digest ──► You
                                                      └──► Instant alert (groups) ──► You
```

## Where to start

| You are... | Start here |
|---|---|
| New to the project | [Overview](docs/user-guide/overview.md) and [How it works](docs/user-guide/how-it-works.md) |
| Setting up your own copy | [Installation](docs/user-guide/installation.md), then [Configuration](docs/user-guide/configuration.md) |
| Something does not work | [Troubleshooting](docs/user-guide/troubleshooting.md) |
| A developer | [Architecture](docs/developer-guide/architecture.md), [Code structure](docs/developer-guide/code-structure.md), [Local development](docs/developer-guide/local-development.md) |
| Working on pipelines and releases | [CI/CD](docs/developer-guide/ci-cd.md) |

The full map is in [`docs/README.md`](docs/README.md). Decisions and their reasons are in [`docs/adr/`](docs/adr/README.md).
