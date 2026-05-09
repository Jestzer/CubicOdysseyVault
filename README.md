# CubicOdysseyVault

A cross-platform Avalonia desktop app that backs up Cubic Odyssey saves and
lets you browse, inspect, tag, restore, and prune them.

Built because Cubic Odyssey's save system is fragile: crashes can zero out
save files, the Quit button sometimes skips the final flush, Steam Cloud has
propagated corrupted saves to other machines, and the game's ~20-slot FIFO
silently buries good manual saves under autosaves. There is no
developer-supplied recovery path. This tool keeps an out-of-band history with
integrity checks and atomic restores.

## How Saves in Cubic Odyssey Work
WIP.

## Features

- **Discovery** — finds Steam roots cross-platform (candidate paths,
  `libraryfolders.vdf` parsing, `HKCU\Software\Valve\Steam\SteamPath` on
  Windows), then walks each `Cubic Odyssey/save/<SteamID32>/<account>/<slot>/`
  layout under Proton compatdata, Windows Documents (incl. OneDrive
  redirection), and Steam Cloud `userdata/.../remote/`.
- **Manual snapshots** — per-slot "Back up now" button. Snapshots are
  copied atomically (each file written as `*.tmp` then renamed) into
  `<backupRoot>/snapshots/<SteamID32>/<account>/<slot>/<UTC-stamp>__<6-hex>/`.
  An identical re-snapshot is detected via a deterministic combined SHA-256
  and skipped — no churn from re-running the same backup.
- **Auto snapshots** — opt-in `FileSystemWatcher` per source with a
  configurable per-key debounce window. Auto refuses to snapshot a slot
  whose integrity check flags it `Corrupted` so a half-written save never
  gets promoted into the store.
- **Tiered retention** — keep N hourly + M daily + K weekly auto snapshots
  (defaults 24/14/8) with UTC-anchored bucket boundaries. Manual,
  Pre-restore, and tagged snapshots are kept forever.
- **Tag / rename / delete** snapshots from the history list. Tagging an
  Auto snapshot promotes it into the "always keep" tier without re-running
  the snapshot.
- **Restore** with a Pre-restore safety net — the live state is captured
  before the swap so the restore is itself undoable. Refuses to run while
  Cubic Odyssey is open (uses `pgrep -f` on Linux/macOS to match the
  wine-hosted .exe; literal process-name match on Windows).
- **Slot card thumbnails** — `screenshot.tga` decoded in-process (no native
  dep) and displayed alongside health and trigger badges.
- **Onboarding wizard** on first run + always-available Settings dialog.
  Settings persist as JSON under `%APPDATA%\CubicOdysseyVault\settings.json`.

## Build & run

Requires the .NET 8 SDK.

```sh
dotnet build CubicOdysseyVault.sln
dotnet test CubicOdysseyVault.sln
dotnet run --project CubicOdysseyVault.Desktop
```

The same commands work on Linux, macOS, and Windows. The `Desktop` project
is the entry point; `Core` holds engine logic (no Avalonia dependency) and
`UI` holds Avalonia views, view-models, and services.

## Project layout

```
CubicOdysseyVault.Core/      pure C# domain logic, no Avalonia refs
  Steam/                     SteamLocator + libraryfolders.vdf parser
  Saves/                     SaveLocator + per-slot enumerator
  Integrity/                 SHA-256 + NULL-block detection + TGA header check
  Snapshots/                 BackupService + manifest + RetentionPolicy
  Tga/                       uncompressed-RGB TGA decoder
  Watching/                  FileSystemWatcher + per-key debounce
  Restore/                   atomic swap + game-running guard
CubicOdysseyVault.UI/        Avalonia 11 + CommunityToolkit.Mvvm 8
  Services/                  AppSettingsService, BackupCoordinator, TgaBitmapLoader
  ViewModels/
  Views/                     MainWindow + Settings/Onboarding/Restore/Tag/Delete dialogs
  Themes/                    DarkTheme.axaml palette
CubicOdysseyVault.Desktop/   entry point (Program.cs, app.manifest)
CubicOdysseyVault.Tests/     89 xUnit tests covering Core
```

## On-disk shape

The backup root (default `%LOCALAPPDATA%\CubicOdysseyVault\`,
configurable in Settings) holds:

```
<backupRoot>/snapshots/
└── <SteamID32>/
    ├── _account/                          ← shared cross-slot data
    │   ├── manifest.json
    │   └── 2026-05-06T14-30-12Z__abc123/
    │       ├── meta.sav
    │       ├── 93_blueprints.sav
    │       └── ...
    └── <accountFolder e.g. 0>/
        └── <slotName e.g. 3>/
            ├── manifest.json              ← per-slot history
            └── 2026-05-06T14-30-12Z__abc123/
                ├── screenshot.tga
                ├── 93_meta.sav
                └── ... (every file in the slot, verbatim)
```

Each `manifest.json` is a JSON document of `Snapshot` records:

```json
{
  "SchemaVersion": 1,
  "Snapshots": [
    {
      "Id": "2026-05-06T14-30-12Z__abc123",
      "CapturedAtUtc": "2026-05-06T14:30:12Z",
      "Trigger": "Manual",
      "Tag": "before-boss",
      "CombinedHash": "sha256:...",
      "FileHashes": { "meta.sav": "...", "screenshot.tga": "..." },
      "TotalBytes": 4928036,
      "Health": "Healthy",
      "SourceKind": "ProtonCompatdata",
      "FolderName": "2026-05-06T14-30-12Z__abc123"
    }
  ]
}
```

Restoring keeps the previous live state in `<slot>.replaced-<UTC>/`
alongside the live folder for one generation, so the restore itself can
be rolled back manually if needed.

## Save sources

| Platform | Path |
|---|---|
| Linux/Proton | `<Steam>/steamapps/compatdata/3400000/pfx/drive_c/users/steamuser/Documents/Cubic Odyssey/save/` |
| Windows | `%USERPROFILE%\Documents\Cubic Odyssey\save\` (also OneDrive Documents if redirected) |
| Steam Cloud | `<Steam>/userdata/<SteamID32>/3400000/remote/Cubic Odyssey/save/` |

Manual override paths can be added in Settings — useful for external
drives or recovered backups from another machine.

## Status

All ten phases of the original implementation plan are done. 89 unit
tests cover the engine; UI exercises were verified by launching the app
and watching it run discovery against this developer's actual save data.
The Windows-side path resolution and registry probe compile but haven't
been runtime-validated on a Windows host yet — runtime smoke-test on
Windows is the one remaining loose end.

- `docs/PLAN.md` — full design spec (read this for the why)
- `docs/HANDOFF.md` — what shipped + what's still loose

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
