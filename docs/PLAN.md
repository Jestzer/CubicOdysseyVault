# Cubic Odyssey Save Vault — Groundwork Plan

> Working title: **CubicOdysseyVault** (descriptive, matches the `OpenFATX` / `ForgeX` naming
> style of nearby projects). Easy to rename before first build.

## Context

Cubic Odyssey (Steam, AppID **3400000**, Atypical Games / Gaijin) is a custom-engine voxel
sandbox. The user enjoys it but the save system is fragile in ways the developer is still
patching: in-place writes that get zeroed on crash, a "Quit" button that can skip the final
save flush, Steam Cloud propagating corrupted local saves to other machines, and a ~20-slot
FIFO that silently buries good manual saves under autosaves. Users on the Steam forums
regularly report multi-hour progress losses with no developer-side recovery path. No
community backup tool exists.

The user wants a **cross-platform Avalonia desktop app** that:
1. Discovers Cubic Odyssey saves on the current machine (Linux/Proton today, Windows later).
2. Snapshots them on demand and automatically.
3. Lets the user browse the snapshot history, view per-slot details, and restore.
4. Ships with the same dark-theme MVVM idioms as the user's other apps (`OpenFATX`, `ForgeX`,
   `yt-dlp-crossplatform` — all in `/run/media/james-work/SSD/My_Programs_SSD/`).

Linux note: the user has not yet launched Cubic Odyssey on this Linux account, so
`steamapps/compatdata/3400000/` does not exist yet. The app must handle "saves don't exist
yet" gracefully and pick them up the moment the user plays.

---

## Established facts (from research)

**Save layout (confirmed via PCGamingWiki + Steam forum recovery threads):**

```
<DocumentsRoot>/Cubic Odyssey/save/<SteamID32>/<account 0|1>/<slot 0..19>/
                                                            ├── screenshot.tga
                                                            ├── meta.sav, 93_meta.sav
                                                            ├── voxworld3_<id>.sav
                                                            ├── vw3_<name>_0.sav
                                                            └── ... (binary blobs)
```

- A "slot" is a **folder**, not a file. Treat as one atomic unit.
- The user assumed worlds and characters are separately backupable. Forum evidence says
  **character + world are bundled per slot**. Plan accordingly; revisit if reverse-engineering
  shows otherwise.
- Up to ~20 slots; FIFO eviction on overflow.
- Two account-level subfolders (`0`, `1`) — likely two character profiles, unconfirmed.
- `<DocumentsRoot>` resolves to:
  - **Windows**: `%USERPROFILE%\Documents\` *or* `%USERPROFILE%\OneDrive\Documents\` (the
    real Documents folder must come from `SHGetKnownFolderPath(FOLDERID_Documents)` to
    handle OneDrive redirection).
  - **Linux/Proton**: `<Steam>/steamapps/compatdata/3400000/pfx/drive_c/users/steamuser/Documents/`.
- **Steam Cloud mirror** also exists at `<Steam>/userdata/<SteamID32>/3400000/remote/`.
  Per research, Cloud has been implicated in independent corruption events — back it up
  separately, do not treat as a safety net.
- Save files are **opaque binary blobs**; format undocumented. Known corruption signature:
  files filled with NULL bytes (interrupted in-place write).

**Engine**: custom voxel engine, Lua-scripted (`/data/scripts/`, `/data/quests/`),
proprietary `.str` localization, PhysX 4.1, BugSplat crash reporter, Steam + EOS + PlayFab
integration. Not Unity, not Unreal.

**Steam install on this machine**: `/run/media/james-work/SSD/Program Files (x86)/Steam/`
(no `userdata/` directory present yet — never logged in via this Steam install on Linux,
or userdata lives elsewhere). Discovery must scan multiple known Steam roots and fall back
to a user-configured override.

---

## User-confirmed scope decisions

| Question | Answer |
|---|---|
| Detail-view depth | **Hybrid** — show metadata + decoded `screenshot.tga` preview now; leave a clean parser hook for v2 |
| Backup trigger | **Both** — manual button + opt-in `FileSystemWatcher` with debounce and integrity verification |
| Retention | **Tiered/generational** — keep last N hourly + last M daily + last K weekly + tagged-forever; defaults user-configurable |

---

## Solution structure (mirrors `OpenFATX`)

```
/run/media/james-work/SSD/My_Programs_SSD/CubicOdysseyVault/
├── CubicOdysseyVault.sln
├── Directory.Build.props        # net8.0 + AvaloniaVersion=11.2.3
├── .gitignore
├── README.md
├── CubicOdysseyVault.Core/      # pure C# domain logic, no Avalonia
│   ├── CubicOdysseyVault.Core.csproj
│   ├── Constants.cs             # AppId = 3400000
│   ├── Steam/
│   │   ├── SteamLocator.cs
│   │   ├── SteamRoot.cs
│   │   └── LibraryFoldersVdfParser.cs
│   ├── Saves/
│   │   ├── SaveLocator.cs
│   │   ├── SaveSource.cs        # discriminated kind: Documents | ProtonCompatdata | SteamCloudRemote | Manual
│   │   ├── SaveSlot.cs
│   │   └── SaveSlotEnumerator.cs
│   ├── Integrity/
│   │   ├── IntegrityChecker.cs  # NULL-block detection, screenshot present, sha256
│   │   └── SlotHealth.cs        # Healthy | Suspicious | Corrupted
│   ├── Snapshots/
│   │   ├── Snapshot.cs
│   │   ├── SnapshotStore.cs     # filesystem-backed
│   │   ├── SnapshotIndex.cs     # JSON manifest per slot
│   │   ├── RetentionPolicy.cs   # tiered/generational
│   │   └── BackupService.cs
│   ├── Restore/
│   │   └── RestoreService.cs    # auto pre-restore snapshot, atomic swap
│   ├── Watching/
│   │   └── SaveWatcher.cs       # FileSystemWatcher + debounce
│   └── Tga/
│       └── TgaDecoder.cs        # minimal uncompressed-TGA → byte[] RGBA
├── CubicOdysseyVault.UI/        # Avalonia MVVM
│   ├── CubicOdysseyVault.UI.csproj
│   ├── App.axaml + App.axaml.cs
│   ├── Assets/                  # icon, etc.
│   ├── Themes/
│   │   └── DarkTheme.axaml      # copy palette from OpenFATX, tweak accent
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs
│   │   ├── MainWindowViewModel.cs
│   │   ├── SaveSlotViewModel.cs
│   │   ├── SnapshotViewModel.cs
│   │   ├── SnapshotHistoryViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── OnboardingViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.axaml + .cs
│   │   ├── OnboardingDialog.axaml + .cs
│   │   ├── SettingsDialog.axaml + .cs
│   │   └── RestoreConfirmDialog.axaml + .cs
│   └── Services/
│       ├── AppSettingsService.cs   # JSON, ApplicationData/CubicOdysseyVault/settings.json
│       ├── BackupCoordinator.cs    # bridges Core services to UI thread
│       └── DialogService.cs
├── CubicOdysseyVault.Desktop/   # entry point
│   ├── CubicOdysseyVault.Desktop.csproj   # OutputType=WinExe
│   ├── Program.cs
│   ├── app.manifest
│   └── Assets/icon.ico
└── CubicOdysseyVault.Tests/     # xUnit
    └── (one file per Core type listed above)
```

Tech stack pinned by `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AvaloniaVersion>11.2.3</AvaloniaVersion>
  </PropertyGroup>
</Project>
```

UI csproj packages (matches `OpenFATX.UI`): `Avalonia`, `Avalonia.Themes.Fluent`,
`Avalonia.Fonts.Inter`, `CommunityToolkit.Mvvm 8.4.0`, `Avalonia.Diagnostics` (Debug).
Desktop csproj: `Avalonia.Desktop`. **No extra dependencies** — TGA decoder, VDF parser,
and FileSystemWatcher debounce are all small enough to hand-roll, keeping the dependency
surface tight (the OpenFATX projects do the same).

---

## Cross-platform discovery strategy

`SteamLocator` returns a list of `SteamRoot { Path, Source }`, scanning in order:

| Platform | Candidate Steam roots |
|---|---|
| Linux  | `~/.steam/steam`, `~/.local/share/Steam`, `~/.var/app/com.valvesoftware.Steam/data/Steam` (Flatpak), `/run/media/*/SSD/Program Files (x86)/Steam` (this user's case) |
| Windows | `HKCU\Software\Valve\Steam\SteamPath`, `%PROGRAMFILES(X86)%\Steam`, `%PROGRAMFILES%\Steam`, `D:\Steam` |
| macOS  | `~/Library/Application Support/Steam` |

For each root: parse `<root>/steamapps/libraryfolders.vdf` (or
`<root>/config/libraryfolders.vdf`) to find additional library paths; also enumerate
`<root>/userdata/*/3400000/` to find which Steam users have played the game.

`SaveLocator` then assembles candidate save sources per Steam root:

1. **Proton compatdata**: `<root>/steamapps/compatdata/3400000/pfx/drive_c/users/steamuser/Documents/Cubic Odyssey/save/<SteamID32>/<account>/<slot>/`
2. **Native Documents** (when running under Windows): resolve via `SHGetKnownFolderPath`
   for both Documents *and* OneDrive Documents.
3. **Steam Cloud remote**: `<root>/userdata/<SteamID32>/3400000/remote/` (treated as a
   second, independent source — corruption can hit Cloud independently).
4. **User overrides** from settings: any folders the user manually pointed at (covers
   weird setups, external drives, recovered backups from another machine).

Each discovered save source produces zero or more `SaveSlot` records via
`SaveSlotEnumerator`, which walks `<source>/<SteamID32>/<account>/<slot>/` patterns and
reads slot metadata (file list, last-modified time, screenshot presence).

---

## Backup pipeline

`BackupService.SnapshotAsync(slot, trigger)` runs:

1. `IntegrityChecker.Inspect(slot)` — confirms slot folder exists, every file is
   non-zero, `screenshot.tga` is present and parses as a valid TGA header, computes
   SHA-256 of each file plus a combined slot hash.
2. If health is `Corrupted` and trigger is `Auto`, **abandon** snapshot and emit a
   warning event (don't propagate corruption into the snapshot store).
3. If combined slot hash equals the most recent snapshot's hash, **skip** (slot hasn't
   actually changed since last snapshot — common with watcher noise).
4. Copy the slot folder to:
   `<backupRoot>/snapshots/<SteamID32>/<account>/<slot>/<UTC-ISO8601>__<short-hash>/`
   preserving file structure verbatim. Files copy to `*.tmp` then rename → resilient
   to interruption.
5. Append a `Snapshot` record to the per-slot `manifest.json`:
   ```json
   {
     "id": "2026-05-06T14-30-12Z__a1b2c3",
     "capturedAt": "2026-05-06T14:30:12Z",
     "trigger": "Auto" | "Manual" | "PreRestore",
     "tag": null,
     "slotHash": "sha256:...",
     "fileHashes": { "meta.sav": "...", ... },
     "totalBytes": 1234567,
     "health": "Healthy",
     "sourceKind": "ProtonCompatdata"
   }
   ```
6. `RetentionPolicy.Prune(slot)` — defaults: keep all `Manual`/tagged forever, last 24
   hourly auto, last 14 daily auto, last 8 weekly auto. Pruning deletes only snapshot
   folders whose IDs are in the prune list; manifest is rewritten atomically.

**`SaveWatcher`**: one `FileSystemWatcher` per discovered save source, recursive, filters
out `.tmp`. Coalesces events into a per-slot debounce window (default 10 s of quiet),
then enqueues a backup job. The watcher is opt-in; toggle lives in settings.

---

## Restore pipeline

`RestoreService.RestoreAsync(slot, snapshot)`:

1. Confirm dialog with snapshot's screenshot, timestamp, size, tag.
2. Detect whether `CubicOdysseySteam.exe` is currently running (Linux: `pgrep`;
   Windows: `Process.GetProcessesByName`). Block restore if so, with a clear message.
3. Take an automatic `PreRestore`-trigger snapshot of the current slot state (so the
   restore itself is undoable).
4. Stage the snapshot files into a sibling `*.restore-tmp/` directory next to the slot.
5. Atomic swap: rename slot → `*.replaced-<timestamp>/`, rename `*.restore-tmp/` → slot.
6. Verify the restored slot passes `IntegrityChecker`.
7. Delete `*.replaced-*` after a configurable cooldown (default: keep one generation).

---

## UI flow

**Main window** (single window, sidebar + content; dialog-callback pattern from
`OpenFATX.UI/Views/MainWindow.axaml.cs`):

- **Sidebar (left)**: discovered Steam users (each `SteamID32` → display name from
  Steam's `localconfig.vdf` if available, otherwise the raw ID). Beneath each user: a
  count of slots, a count of snapshots, a health badge.
- **Slot grid (center)**: cards for each slot of the selected user. Each card shows the
  decoded `screenshot.tga` thumbnail, slot index, last-modified, file count, total
  size, snapshot count, and a health badge (Healthy / Suspicious / Corrupted).
- **Detail panel (right, on slot click)**:
  - "Back up now" button (manual snapshot).
  - "Snapshot history" list — one row per snapshot with timestamp, trigger badge
    (manual/auto/pre-restore), tag, size, restore button, tag/rename button, delete.
  - File-list inspector — names, sizes, and hashes of files in the live slot folder.
  - "Reveal in file manager" button.
- **Toolbar**: Settings, Refresh sources, Open backup folder.
- **Status bar (bottom)**: current watcher status ("Watching 3 sources" / "Idle"),
  last backup time, last error.

**Onboarding (first run)**:

1. Welcome screen explaining the tool and the corruption risks it mitigates.
2. Detected Steam roots + save sources (or "no Cubic Odyssey saves found yet — that's
   fine, we'll watch for them").
3. Pick backup root (default suggested: `LocalApplicationData/CubicOdysseyVault/snapshots`).
4. Enable file-watcher? (default Yes).
5. Retention defaults (default: 24 hourly / 14 daily / 8 weekly + tagged forever).

UI styling **copies the OpenFATX palette verbatim** to start (`#FF2D2D30` main bg,
`#FF252526` sidebar, red accent `#FFC62828`/`#FFE53935`); accent can be retoned later
to differentiate from OpenFATX.

---

## Files to reference / reuse

The implementation should copy patterns (not contents) from these files exactly — they
encode the user's house style:

- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/Directory.Build.props` — solution-wide TFM/Avalonia version
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.sln` — multi-project sln layout
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.UI/App.axaml` — `RequestedThemeVariant="Dark"` + FluentTheme + DarkTheme.axaml include
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.UI/Themes/DarkTheme.axaml` — color palette (copy verbatim, retone accent later)
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.UI/ViewModels/ViewModelBase.cs` — one-liner `ObservableObject` base
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.UI/Services/WindowSettingsService.cs` — JSON persistence pattern (`Environment.SpecialFolder.ApplicationData` + project name + try/catch + silent failure)
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.Desktop/Program.cs` — `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()`
- `/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/OpenFATX.UI/Views/MainWindow.axaml` and `.axaml.cs` — DockPanel/border/StackPanel idioms, `Func<...>?` dialog-callback wiring in `DataContextChanged`

For the data model, follow the same `[ObservableProperty]` pattern visible in
`OpenFATX.UI/ViewModels/MainWindowViewModel.cs` and `FileEntryViewModel.cs`.

---

## Suggested implementation order

A separate executing plan will break this down further. Coarse phasing:

1. **Skeleton** — create the four projects, copy `Directory.Build.props`, App.axaml,
   DarkTheme, Program.cs, ViewModelBase. App launches to an empty `MainWindow`. *Verify:* `dotnet run --project CubicOdysseyVault.Desktop` opens a dark window.
2. **Discovery** — `SteamLocator`, `SaveLocator`, `SaveSlotEnumerator`,
   `LibraryFoldersVdfParser`. Wire to a debug list view in MainWindow. *Verify:* unit
   tests for parser; manually confirm "no slots" message on this machine; create a
   fixture folder mirroring the expected structure to confirm enumeration.
3. **Onboarding + settings persistence** — first-run wizard, `AppSettingsService`,
   pick backup root.
4. **Manual snapshot + integrity check + snapshot store** — "Back up now" works end
   to end; snapshots appear in `<backupRoot>` and in the slot's history list.
5. **TGA decoder + slot/snapshot UI polish** — screenshot thumbnails render in cards.
6. **File watcher + retention policy** — opt-in watcher; pruning runs after each
   snapshot.
7. **Restore + pre-restore snapshot + game-running guard**.
8. **Tag/label + delete for snapshots** — protects manual saves from auto-prune.
9. **Cross-platform polish** — Windows path resolution (Documents + OneDrive
   redirection), build & smoke-test on Windows, registry probe for Steam path.
10. **README + screenshots**.

---

## Verification (end-to-end)

- **Skeleton phase**: `dotnet build CubicOdysseyVault.sln` succeeds, `dotnet test`
  passes (empty), `dotnet run --project CubicOdysseyVault.Desktop` opens the dark window.
- **Discovery**: unit tests cover `LibraryFoldersVdfParser` against a synthesized VDF
  fixture and against a real Steam install if found. Manual: on this machine, app
  reports "no Cubic Odyssey saves discovered yet — they'll show up when you play."
- **First real saves**: launch the game once on Linux to populate
  `compatdata/3400000/`; restart the app and confirm slots appear with the right
  `SteamID32`/account/slot numbers and screenshots.
- **Manual backup**: click "Back up now" on a slot; confirm snapshot folder appears at
  `<backupRoot>/snapshots/<SteamID32>/<account>/<slot>/<timestamp>__<hash>/` with a
  byte-identical copy of every file; manifest entry appears in history list.
- **Integrity guard**: hand-edit a save file to all-NULL bytes; trigger snapshot;
  confirm it's flagged `Corrupted` and not promoted to a "good" snapshot.
- **Watcher**: enable watcher; touch a save file; confirm snapshot fires after
  debounce window; confirm idempotency (no duplicate when hash unchanged).
- **Retention**: synthesize 200 fake snapshots across days; run `RetentionPolicy.Prune`;
  assert the expected hourly/daily/weekly survivors remain plus all tagged ones.
- **Restore**: restore an older snapshot; confirm pre-restore snapshot was taken;
  confirm files match the chosen snapshot byte-for-byte; confirm game refusal when
  process is running.
- **Cross-platform**: on a Windows VM, confirm Steam path discovery via registry,
  Documents path resolves correctly under both standard and OneDrive-redirected
  layouts, and at least one slot shows up if a save exists.

---

## Open risks / things to verify during implementation

- **Save file naming variants**: research surfaced patterns (`voxworld3_<id>.sav`,
  `vw3_<name>_0.sav`, `meta.sav`, `93_meta.sav`) but the exhaustive list is unknown.
  Plan: snapshot the entire slot folder verbatim — never filter. Inspect a real slot
  during phase 4 to confirm.
- **Worlds vs characters separability**: user assumed they're independent. Forum
  evidence says they're bundled per slot. Plan uses slot-as-atomic-unit; if RE during
  implementation reveals a clean character/world split, add a v2 "extract character"
  feature.
- **TGA variant**: Cubic Odyssey's `screenshot.tga` may be uncompressed or RLE-compressed
  TrueColor. Phase 5 needs a real screenshot to confirm. Hand-rolled decoder will start
  with uncompressed 24/32-bit TrueColor (the common case) and add RLE if needed.
- **Steam Cloud authority**: Cloud may overwrite local saves on next game launch,
  including a freshly restored slot. Restore flow should warn the user to disable
  Steam Cloud for the game (or run the game offline) before launching.
- **Account-folder semantics** (`0` vs `1`): unconfirmed whether these are character
  profiles or autosave/manual buckets. Display them as-is until reverse-engineered.
- **`SteamID32` discovery when no userdata exists yet**: cannot determine the user's
  SteamID until Steam actually creates the userdata folder or the game runs once. The
  app must handle the empty-state gracefully and re-scan on demand.
- **No reference saves available locally** for testing phases 4–7 until the user plays
  at least once. Consider creating synthetic-fixture slot folders for early testing,
  then re-validating against real saves.
