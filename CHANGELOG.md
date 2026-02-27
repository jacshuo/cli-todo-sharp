# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`todo desc` command** — new dedicated command for viewing and editing a task's long-form description:
  - `todo desc <INDEX>` — print the current description.
  - `todo desc <INDEX> "text"` — set or replace the description.
  - `todo desc <INDEX> --clear` — remove the description entirely.
- **`--clear-description` flag on `edit`** — `todo edit <INDEX> --clear-description` removes a task's description as part of a broader field edit.

### Fixed

- **Overdue detection now uses end-of-day semantics.** Previously a task was considered overdue the moment its UTC due timestamp passed, meaning a task due "today" would appear as `Overdue` at 1:00 AM even though the day had barely started. The comparison now uses the local calendar date, so a task is only overdue once the due date's calendar day has fully elapsed (i.e. from the next calendar day onward), regardless of the hour or timezone offset.
- **Unicode symbols displayed as `?` on Windows terminals.** `Console.OutputEncoding` is now explicitly set to `UTF-8` at startup, ensuring that symbols such as `✓`, `⚠`, `◯`, `⟳`, `✕`, and `●` render correctly in PowerShell and Command Prompt.

### Changed

- `todo edit` warning message updated to mention the new `--clear-description` option.

---

## [1.0.0] — Initial release
