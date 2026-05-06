# Handoff — pick up from Phase 3

> Updated 2026-05-06 at the end of the onboarding/settings phase.
> Read this first when resuming work.

## Where we are

**Phases 1 (Skeleton), 2 (Discovery), and 3 (Onboarding + settings) are
done.** The solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 30 tests, and `dotnet run --project
CubicOdysseyVault.Desktop` opens the dark window with a top toolbar
(Refresh / Settings / Open backup folder), runs auto-discovery, and on
first launch shows a 2-step onboarding wizard.

What Phase 3 added:

- **`CubicOdysseyVault.UI/Services/AppSettingsService.cs`** — JSON
  load/save under `ApplicationData/CubicOdysseyVault/settings.json`
  with silent failure on errors (mirrors OpenFATX's
  `WindowSettingsService` pattern). Includes `LoadFromFile` / `SaveToFile`
  overloads so tests can use temp paths. `GetSuggestedBackupRoot()`
  returns `LocalApplicationData/CubicOdysseyVault/snapshots`.
- **`AppSettings`** record (same file): `BackupRootPath`,
  `ManualSourceRoots`, `WatcherEnabled`, retention numbers
  (24/14/8 by default), `WatcherDebounceSeconds` (10), and
  `HasCompletedOnboarding` flag.
- **`SettingsViewModel` + `Views/SettingsDialog.axaml(.cs)`** — modal
  settings dialog with Browse... folder picker, manual-source list
  with Add/Remove, watcher toggle, and three retention `NumericUpDown`s.
  Save/Cancel via VM commands; dialog closes via `CloseRequested` Action
  callback wired in `DataContextChanged`.
- **`OnboardingViewModel` + `Views/OnboardingDialog.axaml(.cs)`** —
  2-step wizard. Step 1: welcome + detected-sources summary
  (auto-generated from discovery counts). Step 2: backup-root +
  watcher + retention form (composes `SettingsViewModel` to share
  fields). Pre-fills suggested backup root if empty.
- **`MainWindowViewModel`** — loads `AppSettings` in the ctor; passes
  `ManualSourceRoots` to `SaveLocator.LocateSources`; exposes
  `OpenSettings` and `OpenBackupFolder` `[RelayCommand]`s; auto-fires
  the onboarding wizard after first discovery if
  `!HasCompletedOnboarding` (one-shot per session — `_skipOnboardingThisSession`
  flag prevents re-prompting if the user dismisses the wizard).
- **`MainWindow.axaml`** — added top toolbar with [Refresh] [Settings]
  [Open backup folder] buttons. Refresh moved out of the sidebar.
- **`MainWindow.axaml.cs`** — wires `ShowSettingsDialog`,
  `ShowOnboardingDialog`, and `OpenBackupFolderRequested` callbacks in
  `DataContextChanged` (Func/Action pattern matching
  `OpenFATX/Views/MainWindow.axaml.cs`). Cross-platform file-manager
  open via `xdg-open` / `open` / `explorer.exe`.
- **Tests** — `AppSettingsServiceTests` (5: defaults, round-trip,
  corrupt JSON, dir creation, suggested path). Tests project now
  references the UI project, since the service lives there per
  PLAN.md (matches OpenFATX layout).

## What's next: Phase 4 (Manual snapshot + integrity check + snapshot store)

Per `docs/PLAN.md` "Suggested implementation order" item 4:

1. **`Integrity/IntegrityChecker.cs`** + **`Integrity/SlotHealth.cs`**
   (Healthy / Suspicious / Corrupted) — confirms slot folder exists,
   every file is non-zero, `screenshot.tga` parses as a valid TGA
   header, computes SHA-256 of each file plus a combined slot hash.
2. **`Snapshots/Snapshot.cs`** record — id, captured-at, trigger
   (Auto / Manual / PreRestore), tag, slot/file hashes, total bytes,
   health, source kind.
3. **`Snapshots/SnapshotStore.cs`** — filesystem-backed; copies slot
   folder to
   `<backupRoot>/snapshots/<SteamID32>/<account>/<slot>/<UTC-ISO8601>__<short-hash>/`
   via `*.tmp`-then-rename for crash safety.
4. **`Snapshots/SnapshotIndex.cs`** — JSON `manifest.json` per slot,
   atomically rewritten.
5. **`Snapshots/BackupService.cs`** — orchestrates Inspect →
   skip-if-unchanged → copy → manifest update.
6. **Account-level snapshot pipeline** — parallel to slot snapshots;
   `SaveAccount`'s files (meta.sav, blueprints, servers, stats) on an
   independent lifecycle. Don't bundle into slot snapshots.
7. **UI**: per-slot "Back up now" button (RelayCommand on
   `SaveSlotViewModel`); snapshot history panel in detail view; live
   progress + status updates.

Phase 4 will use the settings already wired in Phase 3:
`_settings.BackupRootPath` (snapshot store root) and
`_settings.HourlySnapshotsKept` etc. (Phase 6 retention pruning).

## Phase 3 design notes worth remembering

- **`SettingsViewModel.ApplyTo(existing)`** preserves
  `HasCompletedOnboarding` and `SchemaVersion` from the existing
  settings rather than overwriting them. The settings dialog isn't
  meant to retoggle the onboarding flag.
- **`OnboardingViewModel.ApplyTo(existing)`** sets
  `HasCompletedOnboarding = true`, signalling the wizard won't
  re-trigger on subsequent launches.
- **`_skipOnboardingThisSession`** — set when the wizard returns null
  (Cancel / window-X / Func not wired). Without this flag, RefreshDiscovery
  could re-prompt indefinitely if the user dismisses without
  completing.
- **Manual sources flow through `SaveLocator.LocateSources(roots,
  manualRoots)`** — Phase 2 already accepted the parameter; Phase 3
  just feeds it from settings.
- **`OpenBackupFolder`** auto-creates the directory if missing
  (`Directory.CreateDirectory`) before invoking the OS file manager —
  prevents "file manager opens an empty error" on first use before
  Phase 4 has populated it.

## Style template — non-negotiable (unchanged)

All Avalonia code in this project must mirror the patterns in
`/run/media/james/SSD/My_Programs_SSD/OpenFATX/`:

- .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm 8.4.0, xUnit 2.4.2
- `[ObservableProperty]` for VM state, `[RelayCommand]` for commands
- ViewModel exposes `Func<...>?` callbacks for dialogs; View wires them
  in `DataContextChanged`
- Dark theme + red accent palette (already copied)
- Settings persisted as JSON under `Environment.SpecialFolder.ApplicationData / "CubicOdysseyVault"`
- Compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`)
- Inter font

## Critical files in this repo

```
PLAN.md                                            full design spec (this dir)
HANDOFF.md                                         this file
../Directory.Build.props                            net8.0 + Avalonia 11.2.3
../CubicOdysseyVault.Core/Constants.cs              AppId + path constants + candidate-roots tables
../CubicOdysseyVault.Core/Steam/                    SteamRoot, SteamLocator, LibraryFoldersVdfParser
../CubicOdysseyVault.Core/Saves/                    SaveSource, SaveAccount, SaveSlot, SaveLayout, SaveLocator, SaveSlotEnumerator
../CubicOdysseyVault.UI/Services/AppSettingsService.cs  JSON persistence + AppSettings record
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml      palette
../CubicOdysseyVault.UI/ViewModels/                 MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings / Onboarding VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml      toolbar + sidebar + slot WrapPanel
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml  modal config dialog
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml first-run wizard
../CubicOdysseyVault.Desktop/Program.cs             entry point
../CubicOdysseyVault.Tests/                         30 tests, all passing
```

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when Cloud sync first materializes the
  `remote/` directory on this machine.
- **Two-account-folder semantics (`0` vs `1`)**: only `0/` exists on
  this account. Plan enumerates dynamically.
- **TGA RLE variant**: only uncompressed RGB observed. Phase 5's TGA
  decoder starts uncompressed-only; if a save with image_type 0x0A
  surfaces, add the RLE branch.
- **Slot file naming variants**: snapshot pipeline must capture every
  file in a slot folder verbatim — never filter by name.
