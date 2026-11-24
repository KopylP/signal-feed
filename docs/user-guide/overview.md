# Overview

**Audience:** anyone who wants to run Signal Feed for themselves. No coding is needed.

## What it does

Telegram has more channels and groups than anyone can read. Signal Feed reads the ones you choose,
asks an AI model to score every post against your own rules (your topics, your examples of a good post),
and sends you only what is worth your time.

You get two kinds of messages in Telegram:

- **A daily digest.** The best posts of the day, a short overview of what matters and which topics repeat,
  and a one-line explanation of what is interesting about each post. Links come from the real posts, never from the model.
- **Instant alerts** for the groups you mark as important. When a discussion bursts or a very important message appears,
  you hear about it within the hour instead of tomorrow.

The agent runs by itself in your own Azure subscription. Nothing is shared with anyone else.

## What you need

| Need | Why |
|---|---|
| An Azure subscription | The agent runs on Azure Functions and keeps its data in Azure Storage |
| An OpenAI API key | Posts are scored and summarized with an OpenAI model |
| A Telegram bot (from `@BotFather`) | The bot delivers the digest and alerts to you |
| A separate Telegram account | The agent reads channels and groups as this account (do not use your main one) |
| Telegram `api_id` and `api_hash` | Issued at [my.telegram.org](https://my.telegram.org) for the reading account |
| About an hour | For the first installation |

## Cost

- Azure: the functions stay within the free grant of the Flex Consumption plan and storage costs pennies.
- OpenAI is the main expense and depends on how many posts you follow.
- A **$5 monthly budget** on the Azure resource group emails you at 80% and 100%.
  It does not cover OpenAI, so watch your OpenAI usage separately.

## Privacy and security

- Secrets (OpenAI key, bot token, Telegram `api_hash`) live in Azure Key Vault. They are not in the code, the repository or the Terraform state.
- OpenAI receives post texts and, for groups, author names and participant usernames.
  This is personal data of third parties, even though the sources are public. Decide whether that is acceptable for the groups you follow.
- The bot token never reaches logs or telemetry.

## Where to go next

1. [How it works](how-it-works.md): what happens to a post from Telegram to your digest.
2. [Installation](installation.md): set it up.
3. [Configuration](configuration.md): make it yours.
