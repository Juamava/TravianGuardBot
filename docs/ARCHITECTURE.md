# Architecture

This document explains how the code is organized and how the bot does its work. If C#, .NET or WPF are new to you, read [`DOTNET_PRIMER.md`](DOTNET_PRIMER.md) first.

## The big picture

```
┌──────────────────────────── WPFUI (Windows app) ───────────────────────────┐
│  XAML views + code-behind bindings (ReactiveUI)                            │
└──────────────┬─────────────────────────────────────────────▲──────────────┘
               │ user actions                                 │ notifications
┌──────────────▼──────────────────── MainCore ────────────────┴──────────────┐
│  UI/ViewModels ──► TaskManager (one task queue per account)                │
│                        │                                                   │
│                    TimerManager (one loop per account, retries)            │
│                        │                                                   │
│                    Behaviors (login check, village switch, logging)        │
│                        │                                                   │
│                    Tasks ──► Commands ──► Parsers ──► ChromeBrowser        │
│                                  │                        (Patchright)     │
│                                  ▼                                         │
│                          AppDbContext (EF Core + SQLite, TBS.db)           │
└────────────────────────────────────────────────────────────────────────────┘
```

- **WPFUI** contains only the windows, the XAML layouts and the bindings.
- **MainCore** contains everything else: the bot logic, the database, the browser automation, and also the ViewModels. Because the ViewModels live here, the UI logic can be tested without WPF.
- **MainCore.Test** contains the tests.

## Projects and folders

| Path | Purpose |
|------|---------|
| `WPFUI/App.xaml.cs` | Entry point. Builds the host and opens `MainWindow`. |
| `WPFUI/Views/` | XAML screens (`Tabs/`, `Tabs/Villages/`, `UserControls/`). Each one mirrors a ViewModel. |
| `MainCore/AppMixins.cs` | Startup wiring: logging, database and dependency injection. |
| `MainCore/Tasks/` | **Tasks**: scheduled units of work, such as `UpgradeBuildingTask` or `LoginTask`. |
| `MainCore/Tasks/Base/` | `BaseTask`, `AccountTask` and `VillageTask`. |
| `MainCore/Commands/` | **Commands**: small reusable steps that tasks call. |
| `MainCore/Commands/Features/` | Game actions: login, upgrade, NPC, quests, hero, train troops. |
| `MainCore/Commands/Navigate/` | Move around the game: switch village, open a building, switch tab. |
| `MainCore/Commands/Update/` | Read the current page and save the data to the database. |
| `MainCore/Commands/Misc/` | `OpenBrowserCommand`. |
| `MainCore/Parsers/` | Static classes that find elements and values on the live page with CSS selectors. |
| `MainCore/Behaviors/` | Middleware that wraps every task or command. These are **not** WPF behaviors. |
| `MainCore/Services/` | Long-lived services: browser, task queue, task loop, logging, `RxQueue`. |
| `MainCore/Services/Scheduling/` | `AccountSchedulerActor`, the task queue of one account. |
| `MainCore/Infrasturecture/Persistence/` | `AppDbContext`, the database. The folder name is misspelled in the code. |
| `MainCore/Infrasturecture/Extensions/` | Helper methods on `AppDbContext`, such as reading settings or getting and adding jobs. |
| `MainCore/Entities/` | Database tables as classes. |
| `MainCore/DTO/` | Plain data objects, with generated mappers between DTOs and entities. |
| `MainCore/Models/` | In-memory models, such as `NormalBuildPlan` and `ChromeSetting`. |
| `MainCore/Enums/` | Setting keys, building types and statuses. |
| `MainCore/Errors/` | Error types used as return values. See [Errors as values](#errors-as-values). |
| `MainCore/Constraints/` | Marker interfaces (`ITask`, `ICommand`, `IAccountConstraint`, ...). Behaviors use them to choose what to wrap. |
| `MainCore/Notifications/` | Messages sent from background code to the UI. |
| `MainCore/UI/ViewModels/` | Screen logic (MVVM). |
| `MainCore/UI/Models/` | Form input and output models, plus FluentValidation validators. |
| `MainCore/UI/Stores/` | Shared UI state: the selected account, the selected village and the visible tabs. |
| `Examples HTML/` | Saved game pages kept for reference while writing parsers. No code uses them. |
| `FAQs/` | End-user guides. |

## Libraries

| Library | Used for |
|---------|----------|
| [Patchright](https://github.com/Kaliiiiiiiiii-Vinyzu/patchright-dotnet) | Browser automation. It is a Playwright fork that is harder to detect. Its namespace is `Microsoft.Playwright`. |
| [Immediate.Handlers](https://github.com/ImmediatePlatform/Immediate.Handlers) | The `[Handler]` pattern for tasks and commands, plus the behavior pipeline. Similar to MediatR, but source-generated. |
| [Injectio](https://github.com/loresoft/Injectio) | Registers services in dependency injection from attributes such as `[RegisterSingleton]`. |
| EF Core + SQLite | Database (`TBS.db`). |
| [FluentResults](https://github.com/altmann/FluentResults) | `Result` / `Result<T>` return values instead of exceptions. |
| [Polly](https://github.com/App-vNext/Polly) | Retries a failed task. |
| [ReactiveUI](https://www.reactiveui.net/) | MVVM: bindable properties, commands and bindings between views and ViewModels. |
| MaterialDesignThemes | Visual style of the WPF controls. |
| FluentValidation | Validates forms (add account, settings). |
| Riok.Mapperly | Generates the DTO and entity mapping code. |
| StronglyTypedId | Typed IDs such as `AccountId` and `VillageId`, so an account ID cannot be passed where a village ID is expected. |
| Serilog | Logging, with one log file per account. |

## Startup

1. `WPFUI/App.xaml.cs` → `OnStartup` calls `AppMixins.GetHostBuilder()`.
2. `MainCore/AppMixins.cs` configures three things:
   - Serilog writes to `./logs/log-{account}-.txt` and to `LogSink`, which shows log lines in the UI.
   - The SQLite database `TBS.db` is registered through a pooled `IDbContextFactory<AppDbContext>`.
   - Dependency injection is filled by `services.AddMainCore()` (generated by Injectio) and `services.AddMainCoreHandlers()` (generated by Immediate.Handlers). A scoped `IChromeBrowser` gives each scope the browser of the current account.
3. The host starts and opens `MainWindow`.
4. `MainViewModel.Load` runs three steps:
   - It checks that Chrome is installed and newer than 114.
   - It creates the database with `EnsureCreated()`, `EnsureCowListTables()` and `EnsureHeroLevelingTable()`.
   - It fills any missing default settings.

> **There are no EF migrations.** `EnsureCreated()` only creates a database that does not exist yet. Columns added later must be added by hand with raw SQL, the way `AppDbContext.EnsureCowListTables()` does it.

## How a task runs

### 1. A task is queued

A task is a class in `MainCore/Tasks/`. There are two kinds:
- **`AccountTask`**: its key is the account ID. Examples: `LoginTask`, `SleepTask`, `StartAdventureTask`.
- **`VillageTask`**: its key is `accountId-villageId`. Examples: `UpgradeBuildingTask`, `TrainTroopTask`.

Each task has an `ExecuteAt` time. `TaskManager` holds one `AccountSchedulerActor` per account, and each actor holds a list of pending tasks. `AddOrUpdate` replaces a task that has the same key, so there is at most one task per key.

When an account logs in (`MainLayoutViewModel`), three things happen:
- The browser opens (`OpenBrowserCommand`).
- `TimerManager.Start` starts the account loop.
- An `AccountInit` notification is sent. `RxQueue.AccountInitHandler` then queues the first tasks: Login, Sleep, StartAdventure, and UpdateVillage and TrainTroop for each village, plus UpgradeBuilding for each village that has build jobs.

### 2. The account loop picks a task

`TimerManager` runs one background loop per account. The loop checks the queue every 100 ms. When the account is **Online** and the earliest task has `ExecuteAt <= now`, the loop does the following:

1. It marks the task as executing.
2. It creates a dependency injection scope for that account (`CustomServiceScopeFactory.CreateScope(accountId)`). Inside this scope, `IChromeBrowser` and the logger belong to that account.
3. It calls the task handler through a `switch` on the task type in `CustomServiceScopeFactory.Execute`.
4. It wraps the call in a **Polly** retry: 3 attempts, 30 s exponential backoff with jitter. The browser page is refreshed before each retry.
5. It reads the `Result` (see the table below).
6. It waits a random delay between the account settings `TaskDelayMin` and `TaskDelayMax`.

> **When you add a new task**, you must add a `case` for it in `CustomServiceScopeFactory.Execute`. If you do not, the loop never runs it.

### 3. Behaviors wrap the task

Before the task code runs, the Immediate.Handlers pipeline sends the call through the behaviors in `MainCore/Behaviors/`:

- `AccountTaskBehavior` checks that the browser is on an in-game page. If the account is logged out, it queues a `LoginTask` and returns `Skip`. After the task, it refreshes the account info, the village list, the hero and the quest data.
- `VillageTaskBehavior` switches to the correct village and updates its storage and buildings. After the task, it updates them again.
- The `*LoggingBehavior` classes log the task name, the commands and the errors.

### 4. The task calls commands, and commands use parsers and the browser

Example: `UpgradeBuildingTask` (`MainCore/Tasks/UpgradeBuildingTask.cs`) repeats these steps:

1. `GetBuildPlanCommand` reads the next job of the build queue from the database. If no job can be built now, the task returns `Skip` and moves `ExecuteAt` to the next possible time.
2. `ToBuildPageCommand` opens the building page (`ToBuildingByLocationCommand`, `SwitchTabCommand`).
3. `HandleResourceCommand` checks whether the village has enough resources. It uses `UpgradeParser.GetRequiredResource`. The possible outcomes are:
   - Missing resources: `UpgradeParser.GetTimeWhenEnoughResource` gives the wait time, and the task returns `Skip` with a new `ExecuteAt`.
   - The storage is too small: the task returns `Stop`.
   - Not enough free crop: the loop tries the next job.
4. `HandleUpgradeCommand` clicks the upgrade button that `UpgradeParser` finds.
5. `UpdateBuildingCommand` reads the building layout again and saves it.

Parsers do **not** parse HTML text. They run CSS selectors on the live Playwright page (`IPage`) and return an `ILocator`, which is a reference to an element. `ChromeBrowser.Click` and `ChromeBrowser.Wait` act on that locator.

### 5. The result decides what happens next

| Result | Effect |
|--------|--------|
| `Ok` / `Skip` | The task is removed from the queue, unless the task moved its own `ExecuteAt`, as `UpgradeBuildingTask` does. |
| `Retry` (or an exception) | Polly retries the task up to 3 times. If it still fails, the bot takes a screenshot and pauses the account. |
| `Stop` | The bot takes a screenshot and pauses the account. |
| `Cancel` | The account pauses. This happens when the user pauses it or the app closes. |

## Errors as values

Methods return `Result` or `Result<T>` (FluentResults) instead of throwing exceptions. The error classes in `MainCore/Errors/` have two roles:

- **Control-flow errors**: `Skip`, `Stop`, `Retry`, `Cancel`. They tell the loop what to do next.
- **Domain errors**: `MissingResource`, `MissingBuilding`, `StorageLimit`, `LackOfFreeCrop`, `NextExecuteError`, `UpgradeBuildingError`, `AccountDuplicate`. They give the reason for a failure.

The calling code checks them with `result.IsFailed` and `result.HasError<StorageLimit>()`.

## Browser

- There is one `ChromeBrowser` per account, cached in `ChromeManager`.
- `ChromeBrowser` starts the **installed Google Chrome** (`Channel = "chrome"`) with a persistent profile in `Data/Cache/<server>/<username>/<proxy or default>`. Because of this, cookies and the session survive restarts.
- The proxy comes from the account `Access` (host, port, user and password). WebRTC is limited so that it cannot leak the real IP address.
- Headless mode is an account setting (`HeadlessChrome`).
- `SleepTask` closes the browser after the configured work time and opens it again later.

## Database

- File: `TBS.db` (SQLite), next to the executable.
- `AppDbContext` has these tables: `Accounts`, `AccountsInfo`, `Accesses` (login and proxy), `AccountsSetting`, `HeroItems`, `Villages`, `Buildings`, `QueueBuildings`, `Jobs` (build queue), `Storages`, `VillagesSetting`, `FarmLists`, `CowLists`, `CowVillages`, `HeroLevelings`.
- Settings are stored as rows (key and value). `FillAccountSettings` and `FillVillageSettings` add the missing defaults at startup. To read a setting, use the extension methods in `Infrasturecture/Extensions`, for example `context.ByName(accountId, AccountSettingEnums.TaskDelayMin)`.
- Code asks `IDbContextFactory<AppDbContext>` for a short-lived context: `using var context = contextFactory.CreateDbContext();`.

## UI (MVVM with ReactiveUI)

- Each screen has a **View** (`WPFUI/Views/.../XTab.xaml`) and a **ViewModel** (`MainCore/UI/ViewModels/.../XViewModel.cs`).
- ViewModels inherit from `ViewModelBase : ReactiveObject`. Their bindable fields use `[Reactive]`.
- Bindings are written in C# in the view code-behind (`XTab.xaml.cs`), inside `WhenActivated`, for example `this.Bind(ViewModel, vm => vm.X, v => v.Box.Text)`. They are not written in XAML.
- `Stores/` hold shared state, such as the selected account and village, so that different tabs stay in sync.
- **Background to UI:** background code calls `rxQueue.Enqueue(new TasksModified(accountId))`. ViewModels listen with `rxQueue.GetObservable<TasksModified>()` and refresh. The message types are in `MainCore/Notifications/`.

## Cow lists (oasis raids)

`RaidCowListTask` (`MainCore/Tasks/RaidCowListTask.cs`) works through a user-defined list of oasis URLs (`CowList` and `CowVillage` tables). For each target it does the following:

1. It skips the target if it was attacked recently (each target has a cooldown).
2. It opens the map tile. `CowOasisParser` checks that the oasis is unoccupied and has no animals.
3. It fills the rally point raid form with the troop plan of that target and sends it.
4. It records the reason for each skipped target. Some reasons are in Spanish, for example "Ocupado" and "Sin tropas".

The task reschedules itself while `KeepRepeating` is on. `OasisCatalog` builds links to `oasis.travibot.com` to find oases near the village. The UI is in `FarmingViewModel.CowLists.cs`.

## Hero leveling ("Poner a levear")

`HeroLevelingTask` (`MainCore/Tasks/HeroLevelingTask.cs`) levels the hero up without letting him die. The on/off flag, the state and the last message live in the `HeroLevelings` table (one row per account, created with raw SQL in `EnsureHeroLevelingTable`). The Hero page of the web admin uses `GET /api/accounts/{id}/hero` and `POST /api/accounts/{id}/hero/leveling/start|stop`. `RxQueue.AccountInitHandler` queues the task again when it is enabled.

Each run does this:

1. If the hero is not home (`HeroParser.IsHeroHome`, the top bar `i.heroHome` icon), it waits until the estimated return or 5 minutes.
2. It reads HP, XP, level, speed and the hero points on `/hero/attributes` (`HeroParser.ReadStats`). If the HP cannot be read, it does nothing and waits 10 minutes.
3. HP >= 50% and an adventure is available: it starts the adventure with the existing adventure commands.
4. HP < 50%: it collects task rewards one at a time and reads the HP again after each one. Leveling up restores full HP, so it stops as soon as the HP is back to 50% or more. The rest of the tasks are kept as an XP reserve. If the HP is still low, it waits 30–60 minutes.
5. HP >= 50% and no adventure: it hunts in the cow list named **Oasis**. If the list is missing or empty, the task disables itself and shows a message. Oases are checked from the closest to the farthest from the hero's village, at most 20 per run. The search position is saved in `HeroLevelings.NextOasisId`, so the next run continues after the oases that were already too strong or empty. After a hunt it continues with the next oasis. It starts again from the closest when the hero levels up, has 10% more HP than when the position was saved, finishes a full lap without a safe oasis, or leveling is turned on again. For each oasis it does this:
   - It reads the tile and skips the oasis if it is occupied or has no animals.
   - It opens the combat simulator with `newdid` set to the hero's village. It clears the village troops that the simulator prefills, keeps only the hero, loads the real HP, strength points and off-bonus points, checks that the defenders match the animals on the tile, and simulates.
   - `HeroLevelingRules.Evaluate` allows the send only if the hero is the only attacker, his loss cell is 0 (`−1` means he dies), at least one animal dies, the health line is readable, and at least 25% HP is left.
   - It then opens the rally point raid form, checks the target coordinates, checks that the hero is available (1), makes sure the hero is the only troop and that raid is selected, and checks the confirmation screen before it confirms.

The automatic `StartAdventureTask` (account setting "Auto start adventure") uses the same rule: it reads HP on `/hero/attributes` first and only starts an adventure with 50% HP or more. With less, or when the HP cannot be read, it waits 30 minutes.

Everything that is unknown or unreadable means the hero is not sent. The pure rules are in `MainCore/Parsers/HeroLevelingRules.cs` and are tested in `MainCore.Test`.

## CI/CD (`.github/workflows`)

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| `test.yml` | Pull request to `main` | Builds a single-file release, tags the next patch version and comments a download link on the PR. The test job is disabled (`if: false`). |
| `release.yml` | Manual | Bumps the minor version, builds, creates a GitHub release and notifies Discord. |
| `patch.yml` | Manual | Same as release, but reuses the latest tag. |

## How-to

### Add a new task

1. Create `MainCore/Tasks/MyTask.cs`. Copy the shape of an existing task:
   ```csharp
   [Handler]
   public sealed partial class MyTask(IChromeBrowser browser /*, SomeCommand.Handler someCommand */)
   {
       public sealed class Task(AccountId accountId, VillageId villageId) : VillageTask(accountId, villageId)
       {
           protected override string TaskName => "My task";
       }

       private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
       {
           // call commands, return Result.Ok() or Skip/Stop/Retry
           return Result.Ok();
       }
   }
   ```
2. Add a `case MyTask.Task` to the `switch` in `MainCore/Services/CustomServiceScopeFactory.cs`.
3. Queue the task from a ViewModel or from another task with `taskManager.AddOrUpdate<MyTask.Task>(new(accountId, villageId))`.

You do not register the handler by hand. Immediate.Handlers generates the registration from `[Handler]`.

### Add a new command

Create a `[Handler] sealed partial class XCommand(...)` with a nested `record Command(...) : IAccountCommand` (or `IVillageCommand`) and a private `HandleAsync`. Inject `XCommand.Handler` where you need it and call `await handler.HandleAsync(new(...), cancellationToken)`.

### Read something new from the page

Add a static method to a parser in `MainCore/Parsers/` that takes an `IPage` and returns an `ILocator` or a value. Use the saved pages in `Examples HTML/` to find the right selectors.
