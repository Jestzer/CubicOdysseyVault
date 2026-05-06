# CubicOdysseyVault

A cross-platform Avalonia desktop app that backs up Cubic Odyssey saves and lets
you browse, inspect, and restore them. Built because Cubic Odyssey's in-place save
writes are fragile: crashes can zero out save files, the Quit button can skip the
final flush, and Steam Cloud can propagate corrupted saves to other machines.

## Status

Phase 1 (skeleton) — solution builds, dark `MainWindow` opens. Discovery, snapshot,
restore, and watcher logic land in subsequent phases.

- `docs/HANDOFF.md` — current state + what to do next
- `docs/PLAN.md` — full design spec

## Build & run

```
dotnet build CubicOdysseyVault.sln
dotnet run --project CubicOdysseyVault.Desktop
```

Requires .NET 8 SDK.

## Commit messages

Every commit subject must start with one of:

| Prefix | Use for |
|---|---|
| `feat:` | a new user-visible feature |
| `fix:` | a bug fix |
| `refactor:` | a change that neither adds a feature nor fixes a bug |
| `test:` | adding or changing tests |
| `doc:` | documentation only (README, comments, design docs) |
| `style:` | formatting, whitespace, code-style tweaks with no behavior change |
| `chore:` | tooling, build, dependencies, repo housekeeping |

Keep the subject short and imperative — e.g. `feat: discover save folders on Linux/Proton`.
