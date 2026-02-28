# cli-todo-sharp

[![CI](https://github.com/jacshuo/cli-todo-sharp/actions/workflows/ci.yml/badge.svg)](https://github.com/jacshuo/cli-todo-sharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/CliTodoSharp.svg)](https://www.nuget.org/packages/CliTodoSharp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CliTodoSharp.svg)](https://www.nuget.org/packages/CliTodoSharp)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com)

A feature-rich, cross-platform CLI task/to-do manager written in **C# (.NET 10)**.  
Rich terminal UI powered by [Spectre.Console](https://spectreconsole.net) — coloured tables, progress bars, status badges, interactive prompts, and more.

```
todo list
                                     Tasks
╭──────┬──────────────────────┬──────────────────┬────────────┬──────────┬─────────╮
│    # │ Title                │      Status      │  Priority  │   Due    │ Tags    │
├──────┼──────────────────────┼──────────────────┼────────────┼──────────┼─────────┤
│    1 │ Write project README │  ⟳ In Progress   │   ● High   │  in 2d   │ #docs   │
│    2 │ Fix login bug        │   ⚠ Overdue      │ ● Critical │ 17d ago  │ #work   │
│    3 │ Buy groceries        │    ✓ Done        │   ● Low    │    —     │ #home   │
╰──────┴──────────────────────┴──────────────────┴────────────┴──────────┴─────────╯
Showing 3 of 3 task(s)  ·  ✓ 1 done  ·  ⚠ 1 overdue
```

---

## Table of Contents

1. [Features](#features)
2. [Project Structure](#project-structure)
3. [Architecture](#architecture)
4. [Prerequisites](#prerequisites)
5. [Build & Run](#build--run)
6. [Commands Reference](#commands-reference)
7. [Task Statuses](#task-statuses)
8. [Priority Levels](#priority-levels)
9. [Data Storage](#data-storage)
10. [Portable Usage](#portable-usage)
11. [Third-party Packages](#third-party-packages)
12. [Key Design Decisions](#key-design-decisions)
13. [Contributing](#contributing)
14. [License](#license)

---

## Features

| Feature | Details |
|---|---|
| Rich task table | Rounded borders, colour-coded status & priority, relative due-date labels |
| Overdue detection | Computed at runtime from `DueDate`; never a stale value in the JSON file |
| Progress bars | Per-status ASCII fill bars + a segment breakdown chart in `todo stats` |
| Interactive prompts | `todo add` prompts for the title if none is supplied on the command line |
| Safe file writes | Atomic write-to-temp-then-rename prevents data corruption on crash/power loss |
| Portable JSON store | Human-readable, git-diffable JSON array; path overridable via flag or env var |
| Confirmation guards | `remove` and `purge` ask for confirmation; bypassable with `--yes` |
| State-machine guards | Invalid transitions (e.g. "done → start") are rejected with a clear error |
| Full DI | Commands receive services via constructor injection (no static state) |
| .NET 10 | Targets the latest framework; primary-constructor syntax throughout |

---

## Project Structure

```
cli-todo-sharp/
├── src/
│   └── CliTodoSharp/
│       ├── CliTodoSharp.csproj          # SDK-style project; NuGet refs
│       ├── Program.cs                   # Entry point: DI setup + Spectre app config
│       │
│       ├── Models/
│       │   ├── TodoTask.cs              # Core domain entity
│       │   ├── TodoStatus.cs            # Enum: Pending | InProgress | Done | Canceled
│       │   └── TaskPriority.cs          # Enum: None | Low | Medium | High | Critical
│       │
│       ├── Services/
│       │   ├── ITaskStorageService.cs   # Persistence abstraction
│       │   ├── JsonTaskStorageService.cs# JSON-file implementation (atomic writes)
│       │   └── TaskManager.cs           # Business logic layer + TaskStats record
│       │
│       ├── Commands/
│       │   ├── BaseCommandSettings.cs   # Shared --storage option
│       │   ├── AddCommand.cs            # todo add
│       │   ├── ListCommand.cs           # todo list / ls
│       │   ├── ShowCommand.cs           # todo show <index>
│       │   ├── StartCommand.cs          # todo start <index>
│       │   ├── DoneCommand.cs           # todo done <index>
│       │   ├── CancelCommand.cs         # todo cancel <index>
│       │   ├── ReopenCommand.cs         # todo reopen <index>
│       │   ├── RemoveCommand.cs         # todo remove / rm <index>
│       │   ├── EditCommand.cs           # todo edit <index>
│       │   ├── StatsCommand.cs          # todo stats
│       │   └── PurgeCommand.cs          # todo purge
│       │
│       ├── Rendering/
│       │   └── TaskRenderer.cs          # All Spectre.Console output helpers
│       │
│       └── Infrastructure/
│           └── TypeRegistrar.cs         # ITypeRegistrar/ITypeResolver for DI bridge
│
├── tests/
│   └── CliTodoSharp.Tests/
│       ├── CliTodoSharp.Tests.csproj    # xUnit + FluentAssertions test project
│       ├── InMemoryTaskStorage.cs       # ITaskStorageService stub for unit tests
│       ├── TaskManagerTests.cs          # 18 TaskManager unit tests
│       └── JsonTaskStorageServiceTests.cs # 7 JSON storage integration tests
│
├── .github/
│   ├── workflows/
│   │   └── ci.yml                       # CI: build + test on Ubuntu/Windows/macOS
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.yml
│   │   └── feature_request.yml
│   └── PULL_REQUEST_TEMPLATE.md
│
├── .vscode/
│   ├── launch.json                      # 24 debug configurations
│   └── tasks.json                       # Build (Debug/Release) + publish tasks
│
├── cli-todo-sharp.sln                   # Solution file
├── global.json                          # Pins .NET SDK version
├── Directory.Build.props                # Shared MSBuild properties
├── .editorconfig                        # C# code-style rules
├── LICENSE                              # MIT
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
└── README.md
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  CLI Layer  (Spectre.Console.Cli)                        │
│  Commands: Add / List / Show / Start / Done / Cancel … │
│  Settings classes carry parsed CLI arguments             │
└─────────────────────┬───────────────────────────────────┘
                       │ constructor injection
┌─────────────────────▼───────────────────────────────────┐
│  Business Logic  (TaskManager)                           │
│  State-machine transitions  ·  Filtering  ·  Stats      │
└─────────────────────┬───────────────────────────────────┘
                       │ ITaskStorageService
┌─────────────────────▼───────────────────────────────────┐
│  Persistence  (JsonTaskStorageService)                   │
│  Atomic JSON read/write  ·  Path resolution chain       │
└────────────────────────────────────────────────────────-─┘

Rendering (TaskRenderer) is called directly by commands –
it has no state and only depends on domain objects.
```

### Dependency Injection bridge

Spectre.Console.Cli instantiates commands through its own `ITypeResolver` interface.  
`Infrastructure/TypeRegistrar.cs` implements that interface by delegating to a standard `Microsoft.Extensions.DependencyInjection` container, so commands can use ordinary constructor injection:

```csharp
// Program.cs – DI wiring
services.AddSingleton<ITaskStorageService>(_ => new JsonTaskStorageService(storagePath));
services.AddSingleton<TaskManager>();
services.AddTransient<AddCommand>();
// …

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
```

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| OS | Windows / macOS / Linux |

Verify your SDK version:
```bash
dotnet --version
# 10.0.x
```

---

## Build & Run

### Development (run from source)

```bash
git clone https://github.com/jacshuo/cli-todo-sharp.git
cd cli-todo-sharp/src/CliTodoSharp
dotnet run -- <command> [options]
```

### Release build

```bash
dotnet build -c Release
./bin/Release/net10.0/todo --help
```

### Run tests

```bash
dotnet test cli-todo-sharp.sln
# or with verbosity:
dotnet test cli-todo-sharp.sln --verbosity normal
```

### Publish as a self-contained single file

```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish/win

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish/linux

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained false -o ./publish/mac
```

After publishing, copy the `todo` (or `todo.exe`) binary to any directory on your `PATH`.

---

## Commands Reference

### `todo add [TITLE] [options]`

Create a new task.  If `TITLE` is omitted, an interactive prompt appears.

| Option | Short | Description | Default |
|---|---|---|---|
| `--priority <LEVEL>` | `-p` | `none \| low \| medium \| high \| critical` | `medium` |
| `--due <DATE>` | | `yyyy-MM-dd` or `yyyy-MM-ddTHH:mm` | — |
| `--description <TEXT>` | `-d` | Multi-line detail text | — |
| `--tags <LIST>` | `-t` | Comma-separated tags | — |
| `--storage <PATH>` | | Override the JSON file path | see [Data Storage](#data-storage) |

```bash
todo add "Refactor auth module" -p high --due 2026-03-15 -t work,backend
todo add                          # interactive title prompt
```

---

### `todo list [options]`  (alias: `ls`)

Show a rich coloured table of tasks.

| Option | Short | Description | Default |
|---|---|---|---|
| `--status <STATUS>` | `-s` | `all \| pending \| inprogress \| done \| canceled \| overdue` | `all` |
| `--tag <TAG>` | `-t` | Show only tasks with this tag | — |
| `--sort <FIELD>` | | `created \| due \| priority \| title \| status` | `created` |
| `--detail` | | Render a full panel per task instead of a table row | `false` |

```bash
todo list
todo ls --status overdue
todo list --tag work --sort priority
todo list --detail
```

---

### `todo show <INDEX>`

Print the full detail panel for a single task (all fields including description and timestamps).

```bash
todo show 2
```

---

### `todo start <INDEX>`

Transition a **Pending** task to **In Progress**.  Records `StartedAt` timestamp.

```bash
todo start 3
```

---

### `todo done <INDEX>`

Mark a **Pending** or **In Progress** task as **Done**.  Records `CompletedAt` timestamp.

```bash
todo done 3
```

---

### `todo cancel <INDEX>`

Abandon a **Pending** or **In Progress** task (sets status to **Canceled**).

```bash
todo cancel 5
```

---

### `todo reopen <INDEX>`

Reset a **Done** or **Canceled** task back to **Pending**.  Clears `StartedAt` and `CompletedAt`.

```bash
todo reopen 5
```

---

### `todo remove <INDEX> [--yes]`  (alias: `rm`)

Permanently delete a task.  Prompts for confirmation unless `--yes` / `-y` is supplied.

```bash
todo remove 4
todo rm 4 --yes     # skip confirmation
```

---

### `todo edit <INDEX> [options]`

Patch any subset of a task's fields.  Only the options you provide are changed.

| Option | Description |
|---|---|
| `--title <TEXT>` | New title |
| `--description <TEXT>` | New description |
| `--priority <LEVEL>` `-p` | New priority |
| `--due <DATE>` | New due date |
| `--clear-due` | Remove the due date |
| `--tags <LIST>` `-t` | Replace all tags |

```bash
todo edit 1 --title "Updated title" --priority critical
todo edit 2 --clear-due
todo edit 3 --tags work,urgent
```

---

### `todo stats`

Display a statistics dashboard with:
- Proportional **segment chart** (each status coloured differently)
- **Progress bars** per status (count / total)
- **Overall completion rate** bar
- Current storage file path

```bash
todo stats
```

---

### `todo purge [--yes]`

Bulk-delete all **Done** and **Canceled** tasks.  Prompts for confirmation unless `--yes` is used.

```bash
todo purge
todo purge --yes
```

---

## Task Statuses

| Status | Icon | Stored | Description |
|---|---|---|---|
| Pending | `◯` | ✔ | Created, not yet started |
| In Progress | `⟳` | ✔ | Work active |
| Done | `✓` | ✔ | Completed |
| Canceled | `✕` | ✔ | Abandoned |
| **Overdue** | `⚠` | ✘ | **Derived** – Pending/InProgress with `DueDate` < now |

> **Why is "Overdue" not stored?**  
> Storing it would create stale values: a task saved as "Overdue" on Monday would still
> claim to be overdue after being completed on Tuesday.  Instead, `TodoTask.IsOverdue` is
> a computed property evaluated at render time, and the JSON only ever contains the four
> real states above.

### Allowed transitions

```
Pending ──────► InProgress ──────► Done
   │                  │
   └──────────────────┴──────────► Canceled
                                       │
Done ◄────────────── Reopen ◄──────────┘
```

---

## Priority Levels

| Level | Symbol | Colour |
|---|---|---|
| Critical | `●` | Bold Red |
| High | `●` | Orange |
| Medium | `●` | Yellow |
| Low | `●` | Steel Blue |
| None | `○` | Grey |

---

## Data Storage

Tasks are persisted as a **pretty-printed JSON array** at:

| Platform | Default path |
|---|---|
| Windows | `%USERPROFILE%\.todo-sharp\tasks.json` |
| macOS / Linux | `~/.todo-sharp/tasks.json` |

The path is resolved in this order (first wins):

1. `--storage <path>` CLI flag
2. `TODO_STORAGE_PATH` environment variable
3. Platform home-directory default above

### Sample `tasks.json`

```json
[
  {
    "id": "e2759fd7-84da-420c-8146-a472612bfc8f",
    "title": "Fix login bug",
    "description": null,
    "tags": ["work", "backend"],
    "status": "pending",
    "priority": "critical",
    "createdAt": "2026-02-27T21:21:00.000Z",
    "dueDate":   "2026-02-10T00:00:00.000Z",
    "startedAt":  null,
    "completedAt":null,
    "updatedAt": "2026-02-27T21:21:00.000Z"
  }
]
```

All `DateTime` values are stored as **UTC ISO-8601**.  This means the file is
timezone-independent and will display correctly when opened on a machine in a different
time zone.

**Atomic writes** — the storage service writes to `tasks.json.tmp` first and then
renames over the target file.  This ensures the JSON is never left in a half-written
state if the process is killed mid-save.

---

## Portable Usage

Because the storage file is plain JSON, you can:

- Place `tasks.json` on a USB drive and pass its path with `--storage`:
  ```bash
  todo list --storage /mnt/usb/mytasks.json
  ```
- Set the env variable once in your shell profile:
  ```bash
  # ~/.bashrc or ~/.zshrc
  export TODO_STORAGE_PATH="$HOME/Dropbox/todo/tasks.json"
  ```
- Commit it to a git repo for a simple, auditable history of every change.
- Copy the file between Windows, macOS, and Linux — all timestamps are UTC and
  all enum values are human-readable strings (`"pending"`, `"high"`, …).

---

## Third-party Packages

| Package | Version | Purpose |
|---|---|---|
| [`Spectre.Console`](https://spectreconsole.net) | 0.49.x | Rich terminal UI — tables, panels, progress bars, prompts, markup |
| [`Spectre.Console.Cli`](https://spectreconsole.net/cli) | 0.49.x | Strongly-typed command-line argument parsing on top of Spectre.Console |
| [`Microsoft.Extensions.DependencyInjection`](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection) | 10.x | Standard .NET DI container; bridges Spectre command resolution with services |

All other functionality (JSON, file I/O, date parsing) uses the .NET BCL — no extra packages needed.

---

## Key Design Decisions

### Why Spectre.Console?
It's the most complete rich-terminal library in the .NET ecosystem.  A single package provides markup colouring, tables, panels, live progress, prompts, and the `Cli` sub-library — no need to stitch together multiple tools.

### Why store "Overdue" as derived, not persisted?
See [Task Statuses](#task-statuses).  Short version: stale status values are a correctness bug waiting to happen.

### Why full GUIDs as IDs?
Short integers (1, 2, 3…) are fine for display but collide when two separate JSON files are merged (e.g. from a phone and a laptop).  GUIDs are unique across machines.  We expose short sequential display indices to the user while using GUIDs internally.

### Why atomic file writes?
If the process is killed between `File.WriteAllText` and the data being fully flushed, the JSON is truncated and unreadable.  Writing to `.tmp` first and then `File.Move(..., overwrite: true)` is an atomic rename on all supported operating systems — the old file is only replaced once the new one is fully written.

### Why primary constructors?
C# 12 / .NET 8+ primary constructors reduce boilerplate DI plumbing without any runtime cost.  Every service and command class in this project uses them.

### Why Microsoft.Extensions.DependencyInjection?
Spectre.Console.Cli has its own resolver interface but no built-in IOC container.  Implementing the two-interface bridge (`TypeRegistrar` / `TypeResolver`) against the standard .NET DI container means all normal patterns (scoped services, factory registrations, etc.) work without coupling to a third-party container.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for:

- Setting up the development environment
- Branch and commit conventions (Conventional Commits)
- How to add tests and run them locally
- The pull-request process

Please also review our [Code of Conduct](CODE_OF_CONDUCT.md) before participating.

---

## License

Distributed under the MIT License. See [LICENSE](LICENSE) for details.
