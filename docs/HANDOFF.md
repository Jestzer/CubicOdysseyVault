# Handoff — pick up from Phase 7

> Updated 2026-05-06 at the end of the restore phase.
> Read this first when resuming work.

## Where we are

**Phases 1–7 are done.** The solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 82 tests, and the desktop app supports
end-to-end restore: clicking Restore on a snapshot history row
opens a confirm dialog with the snapshot's screenshot + metadata,
checks whether Cubic Odyssey is running (refused if it is),
captures a Pre-restore snapshot of the live state, then atomically
swaps the live slot folder with the snapshot's contents — keeping
the previous live state in `<slot>.replaced-<utc>/` for one
generation of safety.

What Phase 7 added:

- **`CubicOdysseyVault.Core/Restore/RestoreResult.cs`** — flat result
  record with `Success`, `BlockedByRunningGame`, `Reason`,
  `PreRestoreSnapshot`, `ReplacedFolderPath`.
- **`CubicOdysseyVault.Core/Restore/GameProcessDetector.cs`** —
  `IsCubicOdysseyRunning()` returns true if Cubic Odyssey is in the
  process list. Windows uses `Process.GetProcessesByName`; Linux/macOS
  shells out to `pgrep -f CubicOdysseySteam` so wine-hosted processes
  match by command line. 2 s timeout on the pgrep child; any error
  returns false (we'd rather over-permit restore than lock the user
  out from a probe failure).
- **`CubicOdysseyVault.Core/Restore/RestoreService.cs`** —
  `RestoreSlot(slot, snapshot, backupService)` orchestrates:
  1. Game-running check (via injected `Func<bool>` for tests).
  2. Pre-restore snapshot via the injected `BackupService`
     (tagged `"Pre-restore"`, trigger `PreRestore` — both are in
     retention's "always keep" set).
  3. Stage the snapshot's files into `<slot>.restore-tmp/` via
     `SnapshotStore.CopyFilesAtomically`.
  4. `Directory.Move` live → `<slot>.replaced-<utc>/`, then staging
     → live. If the second move fails, the original is moved back.
  5. `CleanupPreviousReplaced` runs *before* the new replaced dir
     is created — keeps one generation per PLAN.md, so the disk
     doesn't accumulate replaced copies forever.
- **`CubicOdysseyVault.UI/Services/BackupCoordinator`** — gained
  `RestoreSlotAsync(slot, snapshot)` and `GetSlotSnapshotFolder(...)`
  (the latter exposed so the confirm dialog can load
  `screenshot.tga` from the snapshot folder).
- **`CubicOdysseyVault.UI/ViewModels/SnapshotViewModel`** — gained
  `OnRestoreRequested: Func<Snapshot, Task>?` and a `[RelayCommand]
  Restore` that invokes it. Wired by `SaveSlotViewModel` whenever a
  snapshot VM is constructed (initial discovery + insertions
  after a successful BackUp).
- **`CubicOdysseyVault.UI/ViewModels/SaveSlotViewModel`** — gained
  `OnRestoreRequested: Func<SaveSlot, Snapshot, Task>?` set by
  `MainWindowViewModel`; bridges the snapshot VM's callback to the
  main orchestrator with the slot context attached.
- **`CubicOdysseyVault.UI/ViewModels/RestoreConfirmViewModel`** —
  carries snapshot metadata + decoded screenshot bitmap; runs
  `GameProcessDetector.IsCubicOdysseyRunning()` in its ctor and
  exposes `IsGameRunning`, `CanRestore`, plus a `RecheckGame`
  command. `Confirm` and `Cancel` set `Confirmed` and invoke
  `CloseRequested`.
- **`CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml(.cs)`** —
  modal dialog with hero screenshot, snapshot detail grid, a yellow
  warning panel when `IsGameRunning` is true (with Re-check button),
  and the Restore / Cancel pair.
- **`CubicOdysseyVault.UI/ViewModels/MainWindowViewModel`** —
  `HandleRestoreSlotAsync(slot, snapshot)` shows the confirm dialog,
  pauses watchers (so file-system events from the swap don't fire
  spurious Auto snapshots), runs the coordinator's restore, then
  calls `RefreshDiscoveryAsync` (which restarts watchers and
  re-enumerates the slot's freshly restored file list).
- **`MainWindow.axaml`** — snapshot history row gained a Restore
  button on the right; layout reshaped from a vertical `StackPanel`
  to a 2-column `Grid` so the button stays vertically centered next
  to the metadata text.
- **Tests** — 5 new in `RestoreServiceTests`: round-trip
  restore, game-running blocked, pre-restore captures pre-state,
  one-generation replaced cleanup, missing-snapshot graceful
  failure. Total: 82.

## What's next: Phase 8 (tag / rename / delete snapshots)

Per `docs/PLAN.md` item 8:

1. UI for marking a snapshot with a tag — turns it into "always keep"
   in the retention policy without needing a Manual trigger
   re-snapshot.
2. UI for deleting a snapshot (with a guard: can't delete the
   only snapshot, optionally; or just confirm).
3. UI for renaming the tag.
4. Persistence flows through the existing manifest rewrite path.
5. Optional: bulk operations (select multiple, delete all auto >
   30 days, etc.).

Wiring lives in `BackupCoordinator` + `SnapshotViewModel`'s context
menu or extra buttons in the history row.

## Phase 7 design notes worth remembering

- **Game-running check uses `pgrep -f`** on Linux because Cubic
  Odyssey runs as a wine-hosted .exe — the literal process name
  doesn't appear in the system process list. `pgrep -f` searches the
  full command line, so `pgrep -f CubicOdysseySteam` matches the
  wine wrapper plus argv. Any failure returns false — better to let
  a restore through than lock it out from a failed probe.
- **Pre-restore snapshot is always the first thing that runs**, even
  before the game-running check completes its rollback path. The
  PreRestore trigger is in retention's "always keep" set so the
  rollback survives even on a slot that's hammered with auto
  snapshots after a restore.
- **One generation of `*.replaced-*`**: the policy is "delete any
  prior `<slot>.replaced-*` siblings *before* renaming the new
  one." So at most one replaced folder exists per slot at any time.
  This is intentional rather than time-based pruning — it's simple,
  predictable, and keeps disk usage bounded.
- **Atomic swap fallback**: if the staging-→-live `Directory.Move`
  fails (rare, but e.g. a file was opened during the swap), we
  attempt to move the replaced folder back to live. Best effort —
  the user might end up with a half-replaced state on truly weird
  filesystems, but Linux tmpfs/ext4 and Windows NTFS handle the
  rename swap atomically.
- **Watchers paused during restore**: `MainWindowViewModel.StopWatchers()`
  before the coordinator call; `RefreshDiscoveryAsync` at the end
  rebuilds them. Without the pause, the rename + copy events would
  fire the Auto trigger, which would create a redundant snapshot of
  the just-restored content.
- **`HandleRestoreSlotAsync` is set on every slot VM via
  `OnRestoreRequested = HandleRestoreSlotAsync;`** — instance method
  reference, captures `this` (the MainWindowViewModel). Since
  `DiscoverSync` is now an instance method on the VM (not static),
  this works cleanly.

## Style template — non-negotiable (unchanged)

- .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm 8.4.0, xUnit 2.4.2
- `[ObservableProperty]` for VM state, `[RelayCommand]` for commands
- ViewModel exposes `Func<...>?` callbacks for dialogs; View wires
  them in `DataContextChanged`
- Dark theme + red accent palette
- Settings persisted as JSON under `Environment.SpecialFolder.ApplicationData / "CubicOdysseyVault"`
- Compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`)
- Inter font

## Critical files in this repo

```
PLAN.md                                            full design spec (this dir)
HANDOFF.md                                          this file
../Directory.Build.props                            net8.0 + Avalonia 11.2.3
../CubicOdysseyVault.Core/Constants.cs              AppId + path constants + candidate-roots tables
../CubicOdysseyVault.Core/Steam/                    SteamRoot, SteamLocator, LibraryFoldersVdfParser
../CubicOdysseyVault.Core/Saves/                    SaveSource, SaveAccount, SaveSlot, SaveLayout, SaveLocator, SaveSlotEnumerator
../CubicOdysseyVault.Core/Integrity/                SlotHealth, IntegrityChecker, IntegrityReport
../CubicOdysseyVault.Core/Snapshots/                Snapshot, SnapshotTrigger, SnapshotIndex, SnapshotStore, RetentionPolicy, BackupService, BackupResult
../CubicOdysseyVault.Core/Tga/                      TgaImage, TgaDecoder
../CubicOdysseyVault.Core/Watching/                 SlotKey, SaveWatcher
../CubicOdysseyVault.Core/Restore/                  RestoreResult, GameProcessDetector, RestoreService
../CubicOdysseyVault.UI/Services/                   AppSettingsService, BackupCoordinator, TgaBitmapLoader
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml      palette
../CubicOdysseyVault.UI/ViewModels/                 MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings / Onboarding / Snapshot / RestoreConfirm VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml      toolbar + sidebar + slot WrapPanel + right detail panel
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml  modal config dialog
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml first-run wizard
../CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml restore confirm + game-running guard
../CubicOdysseyVault.Desktop/Program.cs             entry point
../CubicOdysseyVault.Tests/                         82 tests, all passing
```

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when Cloud sync first materializes
  the directory.
- **TGA RLE variant**: only uncompressed RGB observed.
- **Two-account-folder semantics (`0` vs `1`)**: only `0/` exists on
  this account.
- **`pgrep -f CubicOdysseySteam` matches the wine-hosted Cubic Odyssey
  process** — confirmed by reasoning, not yet by an actual run on
  this account. If the match fails on a real run, the restore guard
  silently lets restores through. If it false-positives (e.g. on a
  process that happens to contain "CubicOdysseySteam" in its
  command line for unrelated reasons), restores are blocked. Verify
  next time the user has the game open.

## Things deferred

- **Account-level restore**: only slot restore is wired in Phase 7;
  account-level snapshots can be inspected but not yet restored
  through the UI. PLAN.md doesn't explicitly call account restore
  out, but consistency suggests adding it in Phase 8 alongside the
  tag / rename / delete work.
- **Steam display name** from `localconfig.vdf`: still raw SteamID32.
- **Promote Cloud sources once `remote/` materializes**: needs a
  re-scan or watcher on the parent.
- **Live "current state" health** vs "latest snapshot health".
