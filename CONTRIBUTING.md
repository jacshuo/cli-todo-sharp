# Contributing to cli-todo-sharp

Thank you for your interest in contributing! This document explains how to get
involved, what standards we follow, and how to submit changes.

---

## Table of Contents

- [Contributing to cli-todo-sharp](#contributing-to-cli-todo-sharp)
  - [Table of Contents](#table-of-contents)
  - [Code of conduct](#code-of-conduct)
  - [Getting started](#getting-started)
  - [Development setup](#development-setup)
    - [Prerequisites](#prerequisites)
    - [Build](#build)
    - [Run from source](#run-from-source)
    - [Run tests](#run-tests)
    - [Build release binaries](#build-release-binaries)
  - [Project structure](#project-structure)
  - [Making changes](#making-changes)
  - [Testing](#testing)
  - [Coding style](#coding-style)
  - [Commit messages](#commit-messages)
  - [Opening a pull request](#opening-a-pull-request)
  - [Reporting bugs / requesting features](#reporting-bugs--requesting-features)

---

## Code of conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
By participating you agree to uphold it.  Report unacceptable behaviour to the
project maintainers via GitHub.

---

## Getting started

1. **Fork** the repository on GitHub.
2. **Clone** your fork locally:
   ```bash
   git clone https://github.com/<your-handle>/cli-todo-sharp.git
   cd cli-todo-sharp
   ```
3. Add the upstream remote so you can pull future changes:
   ```bash
   git remote add upstream https://github.com/jacshuo/cli-todo-sharp.git
   ```

---

## Development setup

### Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.x (see `global.json`) | Build & run |
| [VS Code](https://code.visualstudio.com) | latest | Recommended editor |
| [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) | latest | VS Code extension |

> **Note:** The SDK version is pinned in `global.json`.  Running `dotnet --version` after
> installing the SDK should show a `10.0.x` version.

### Build

```bash
dotnet build cli-todo-sharp.sln
```

### Run from source

```bash
cd src/CliTodoSharp
dotnet run -- list
dotnet run -- add "My task" --priority high
```

### Run tests

```bash
dotnet test cli-todo-sharp.sln
```

### Build release binaries

Run the VS Code task **"publish – all platforms"** (`Ctrl+Shift+P` → *Tasks: Run Task*),
or from the terminal:

```bash
# Example: Windows self-contained single-file exe
dotnet publish src/CliTodoSharp/CliTodoSharp.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true \
  -o publish/win-x64
```

---

## Project structure

```
cli-todo-sharp/
├── src/CliTodoSharp/          ← Main executable project
│   ├── Models/                ← Domain types (TodoTask, enums)
│   ├── Services/              ← Business logic + storage abstraction
│   ├── Commands/              ← One file per CLI command
│   ├── Rendering/             ← All Spectre.Console output helpers
│   └── Infrastructure/        ← DI bridge for Spectre.Console.Cli
├── tests/CliTodoSharp.Tests/  ← xUnit test project
├── .github/
│   ├── workflows/ci.yml       ← GitHub Actions CI
│   ├── ISSUE_TEMPLATE/
│   └── PULL_REQUEST_TEMPLATE.md
├── .editorconfig              ← Code style rules
├── Directory.Build.props      ← Global MSBuild settings
├── global.json                ← SDK version pin
└── cli-todo-sharp.sln         ← Solution file
```

---

## Making changes

1. Create a feature branch from `main`:
   ```bash
   git checkout -b feat/my-new-feature
   # or
   git checkout -b fix/issue-123-crash-on-purge
   ```
2. Make your changes in small, focused commits (see [Commit messages](#commit-messages)).
3. Keep the scope of a PR focused on **one** concern.  Large refactors that mix
   unrelated changes are harder to review and more likely to introduce regressions.

---

## Testing

- Every new feature or bug fix should come with a corresponding test.
- Tests live in `tests/CliTodoSharp.Tests/`.
- Use the `InMemoryTaskStorage` stub for `TaskManager` tests — no real files.
- Use a temp file (cleaned up in `IDisposable.Dispose`) for `JsonTaskStorageService` tests.
- Run the full suite before opening a PR:
  ```bash
  dotnet test cli-todo-sharp.sln --verbosity normal
  ```
- CI runs on Ubuntu, Windows, and macOS — make sure your changes don't introduce
  platform-specific assumptions (e.g. path separators, line endings).

---

## Coding style

Style is enforced by `.editorconfig`.  Run `dotnet build` — the compiler surfaces
any violations as warnings (errors in CI).

Key conventions:
- **Primary constructors** for DI (avoids boilerplate fields).
- **File-scoped namespaces** (`namespace Foo.Bar;`).
- **`var`** only when the type is obvious from the right-hand side.
- **`using` directives** outside the namespace, sorted (System first).
- Private fields named `_camelCase`; constants named `PascalCase`.
- All public API has XML doc comments (`/// <summary>`).
- Comments inside methods explain *why*, not *what* (the code shows the what).

---

## Commit messages

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary>

[optional body]
[optional footer: Fixes #123]
```

| Type | When to use |
|---|---|
| `feat` | New feature |
| `fix` | Bug fix |
| `refactor` | Code change with no behaviour change |
| `test` | Adding or updating tests only |
| `docs` | Documentation only |
| `chore` | Build scripts, CI, tooling |
| `perf` | Performance improvement |

Examples:
```
feat(commands): add `search` command for full-text task search
fix(storage): handle BOM in UTF-8 JSON files on Windows
chore(ci): pin actions/checkout to v4
docs(readme): add animated demo gif
```

---

## Opening a pull request

1. Push your branch to your fork:
   ```bash
   git push origin feat/my-new-feature
   ```
2. Open a pull request against `main` on the upstream repository.
3. Fill in the [PR template](.github/PULL_REQUEST_TEMPLATE.md).
4. Make sure all CI checks pass.  A maintainer will review your PR and may
   request changes.
5. Once approved, it will be merged with a squash or merge commit.

---

## Reporting bugs / requesting features

- **Bugs:** use the [Bug Report](.github/ISSUE_TEMPLATE/bug_report.yml) issue template.
- **Features:** use the [Feature Request](.github/ISSUE_TEMPLATE/feature_request.yml) template.
- Please search existing issues before opening a new one.

---

Thank you for contributing! 🎉
