# TravianGuardBot

A Windows desktop bot for [Travian](https://www.travian.com/). It drives a real Google Chrome window per account and automates the repetitive parts of the game: building queues, adventures, quests, troop training, NPC trades, farm lists and oasis raids ("cow lists").

The app is written in C# on .NET 10. The UI uses WPF, and the bot automates Chrome with Patchright, a fork of Playwright.

## Features

**Per account:**
- Login, with a cookie banner and help popups dismissed automatically.
- Sleep and wake cycles: the bot closes the browser after the configured work time and reopens it later.
- Hero: start adventures, assign attribute points and use inventory items.
- Claim daily quests.

**Per village:**
- Build queue: upgrade buildings and resource fields from a user-defined list of jobs.
- Instant upgrade (complete immediately).
- NPC resource exchange.
- Train troops.
- Evade attacks: every troop at home leaves as reinforcement to an ally just before the attacks land, and comes back after the last one (trains of attacks included).
- Claim village quests.

**Farming:**
- Refresh and start Travian's built-in farm lists.
- Cow lists: raid a user-defined list of oases. Each target is checked first and is skipped when the oasis is occupied or has animals. Each target has its own troop plan and cooldown.

See [`FAQs/`](FAQs/) for user guides (installing, adding accounts, build queues, settings).

## Requirements

- Windows 10 version 2004 (build 19041) or newer. WPF only runs on Windows.
- Google Chrome **newer than 114**, installed normally. The bot finds Chrome through the Windows registry and drives it. It does not use a separate chromedriver.
- To build from source: the [.NET 10 SDK](https://dotnet.microsoft.com/download). CI uses `10.0.400`.

## Build and run

```bash
dotnet restore --locked-mode
dotnet build TravBotSharp.sln
dotnet run --project WPFUI
```

To build a single-file release executable (`TravianBotSharp.exe`):

```bash
dotnet publish WPFUI -c Release --self-contained -p:PublishSingleFile=true
```

At runtime the app writes these files next to the executable:

| Path | Content |
|------|---------|
| `TBS.db` | SQLite database: accounts, villages, settings, build queues, farm and cow lists |
| `logs/log-<account>-<date>.txt` | One log file per account |
| `Screenshots/` | Screenshots taken when a task fails |
| `Data/Cache/<server>/<user>/...` | Chrome profile per account (cookies, session) |

## Tests

```bash
dotnet test
```

Only a smoke test exists today: `HostingTest` checks that the dependency injection container can be built. The parser test base (`MainCore.Test/Parsers/ParserTestBase.cs`) expects HTML fixtures in `MainCore.Test/HtmlFiles/`, but that folder does not exist yet. The CI test job is disabled in `.github/workflows/test.yml`.

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): how the code is organized and how a task runs from start to end.
- [`docs/DOTNET_PRIMER.md`](docs/DOTNET_PRIMER.md): the C#, .NET and WPF concepts you need to read this codebase, for developers new to the stack.

## Contributing

Commit messages follow Conventional Commits (`type(scope): description`). See [`.github/instructions/copilot-commit.instructions.md`](.github/instructions/copilot-commit.instructions.md).
