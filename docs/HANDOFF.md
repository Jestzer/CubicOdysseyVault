# Handoff — all phases shipped

> Updated 2026-05-06 at the end of Phase 10. See `README.md` for the
> user-facing description; this file is for the next person picking
> up follow-up work.

## Where we are

**All 10 phases of `docs/PLAN.md` are done.** The solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 89 tests on Linux, and the discovery layer
now resolves Windows save sources end-to-end: Documents (via
`SHGetKnownFolderPath` through .NET's `SpecialFolder.MyDocuments`,
which follows OneDrive Backup-PC redirection automatically) plus a
defensive non-redirected `%USERPROFILE%\Documents\` and explicit
`%USERPROFILE%\OneDrive\Documents\` fallback, all deduped by
canonical path. Steam install is probed from
`HKCU\Software\Valve\Steam\SteamPath` via `Microsoft.Win32.Registry`
(in the .NET 8 BCL — no NuGet package needed). Windows runtime
smoke-test still pending (no VM at hand on this dev box).

What Phase 9 added:

- **`SaveLocator.AddDocumentsSource` (Windows)** — replaces the
  Phase 2 stub. Adds three candidates:
  - `Environment.GetFolderPath(SpecialFolder.MyDocuments)` —
    canonical, follows OneDrive redirection.
  - `%USERPROFILE%\Documents` — non-redirected fallback for games
    that ignore the redirect.
  - `%USERPROFILE%\OneDrive\Documents` — explicit OneDrive path,
    in case the redirect didn't propagate.
  Each candidate that exists is added as a `Documents`-kind source;
  duplicates collapse via the existing `DedupByCanonicalPath` pass.
- **`SteamLocator.ReadWindowsRegistryPaths`** — replaces the Phase 2
  empty stub. `Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath")`,
  gated by `OperatingSystem.IsWindows()` (the type throws
  `PlatformNotSupportedException` on non-Windows). Any read failure
  swallows and returns empty, so a registry hiccup doesn't block
  discovery on the candidate-path layer.
- **No new tests**: both code paths only exercise on Windows; the
  unit-test suite runs on Linux. Worth covering in Phase 10's
  Windows smoke pass once a VM is available.

What Phase 8 added:

- **`BackupService` tag/delete operations** — `UpdateSlotSnapshotTag`,
  `DeleteSlotSnapshot`, plus `UpdateAccountSnapshotTag` and
  `DeleteAccountSnapshot` for the account-level pipeline.
  Empty/whitespace tag values clear; otherwise the trimmed value is
  stored. Delete removes the snapshot folder (best-effort) and the
  manifest entry; both go through the existing atomic `*.tmp`-rename
  manifest save.
- **`BackupCoordinator`** async wrappers for the four operations.
- **`SnapshotViewModel`** — `EditTag` and `Delete` `[RelayCommand]`s
  alongside the existing `Restore`. Each invokes a `Func<Snapshot, Task>?`
  callback set by the parent `SaveSlotViewModel`.
- **`SaveSlotViewModel`** — `OnEditTagRequested` and `OnDeleteRequested`
  callbacks routed up to `MainWindowViewModel` with the slot context
  attached, mirroring the existing restore wiring.
- **`TagEditViewModel`** + **`Views/TagEditDialog.axaml(.cs)`** — small
  modal with a `TextBox` and three buttons (Cancel, Clear tag, Save).
  Returns a tri-state via `Confirmed` + `Result`: null = cancelled,
  "" = clear, anything else = set.
- **`DeleteConfirmViewModel`** + **`Views/DeleteConfirmDialog.axaml(.cs)`** —
  modal showing the snapshot's metadata + the trigger label, plus
  yellow warnings if the snapshot is `PreRestore` (deleting it makes
  the most recent restore irreversible) or has a tag (deleting
  removes it along with the snapshot).
- **`MainWindowViewModel.HandleSlotEditTagAsync`** + **`HandleSlotDeleteAsync`** —
  show the relevant dialog, call the coordinator on confirm, refresh
  discovery on success. Status bar reflects the outcome.
- **`MainWindow.axaml`** — snapshot history row reshaped from a
  Grid-with-side-button layout to a vertical `StackPanel` ending in
  a horizontal `Restore` / `Tag...` / `Delete` action row.
- **Tests** — 7 new in `SnapshotTagDeleteTests`: tag round-trip,
  empty/whitespace clears the tag, unknown id returns false, delete
  removes folder + manifest entry, account delete works, plus a
  retention interaction test (tagging an Auto snapshot promotes it
  to "always keep" so it survives a subsequent prune). Total: 89.

## What's left (loose ends, not blocking)

- **Windows runtime smoke-test**: the Phase 9 path/registry code
  compiles cross-platform but hasn't been exercised on an actual
  Windows host. Next time someone runs the app on Windows: confirm
  `SteamLocator.Locate` returns at least one root from the registry
  probe, and `SaveLocator.LocateSources` surfaces a `Documents`-kind
  source matching `<UserProfile>\Documents\Cubic Odyssey\save\`.
- **Validate inferred Steam Cloud `remote/` layout** when sync
  materializes the directory. Currently `SaveLocator` builds the
  path by combining `userdata/<id>/<app>/remote/Cubic Odyssey/save`,
  inferred from `remotecache.vdf` paths.
- **Account-level snapshot UI**: account snapshots are stored and
  pruned correctly but the right detail panel only surfaces slot
  history. Adding an account-history flyout (or merging into the
  existing card) is the obvious next polish pass.
- **Steam display name** from `<root>/userdata/<SteamID32>/config/localconfig.vdf`:
  sidebar still shows raw `SteamID32`. Small VDF parsing job.
- **Promote Cloud sources once `remote/` materializes**: needs a
  re-scan or a watcher on the `userdata/<id>/<app>/` parent.
- **Live "current state" health** vs. "latest snapshot health":
  could surface as a "Re-check" button in the right detail panel.
- **Bulk operations** ("delete all auto > 30 days", "tag-all",
  multi-select) — building blocks exist; UI doesn't.
- **Garbage-collect orphan snapshot folders** that survived a
  failed `Directory.Delete` during snapshot deletion.
- **Screenshots in the README** — not captured here because the
  GUI-launch flow doesn't have an automated screenshot step yet.

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when sync materializes the directory.
- **TGA RLE variant**: only uncompressed RGB observed.
- **Two-account-folder semantics (`0` vs `1`)**: only `0/` exists.
- **`pgrep -f CubicOdysseySteam` on Linux/Proton**: confirmed by
  reasoning, not by an actual run.
- **Windows process name** for `Process.GetProcessesByName`: assumed
  to literally be `CubicOdysseySteam` (no `.exe`).
- **Windows registry probe** + **OneDrive Documents redirect**:
  code is in place but hasn't been smoke-tested on a Windows host.
  Likely just-works (both APIs are well-documented) but verify the
  first time the project is run on Windows.

## Phase 9 design notes worth remembering

- **Why `Microsoft.Win32.Registry` doesn't need a NuGet package**: the
  type lives in the .NET 8 BCL for cross-platform targets and throws
  `PlatformNotSupportedException` if you call it on non-Windows. The
  `OperatingSystem.IsWindows()` gate prevents that throw, and the
  `try/catch` swallows any registry weirdness (key missing, ACL'd
  off, etc.) so a registry hiccup doesn't block discovery — the
  candidate-path list still runs.
- **Three Documents candidates for one redirect**: `MyDocuments`
  alone *should* be enough on a healthy machine, but real-world
  OneDrive setups are messy. Backup-PC can flip a folder mid-session,
  some users have legacy data in `%USERPROFILE%\Documents` after
  enabling redirect, and games occasionally write to the
  un-redirected location. Cheap to probe all three; the canonical-path
  dedup at the end of `LocateSources` collapses anything that
  resolves to the same physical directory.

## Phase 8 design notes worth remembering

- **Empty-string tag = clear**: `BackupService.UpdateTag` passes
  `string.IsNullOrWhiteSpace(newTag) ? null : newTag.Trim()`, so the
  caller can pass an empty string from a TextBox without juggling
  null. The `TagEditDialog` returns `null` only for "Cancel"; "Save"
  with an empty value and "Clear tag" both return `""`.
- **Three-state tag dialog**: distinguishing Cancel from Clear matters
  because retention sees them differently — a tagged snapshot is
  always kept, an untagged Auto snapshot can be pruned. Cancel means
  no change; Clear is an explicit user action that may make the
  snapshot eligible for pruning on the next snapshot.
- **Delete dialog warns about Pre-restore + tagged**: the warning
  is informational, not blocking. Users who have a reason to delete
  a Pre-restore (e.g. they're confident the restore was good and want
  to free space) can still proceed. Same for tagged.
- **No "delete only auto > N days" bulk action yet**: PLAN.md left
  this as optional. Worth adding in Phase 10 if the snapshot store
  gets crowded; the building blocks (`DeleteSlotSnapshot` keyed by
  id) are in place.
- **Delete uses best-effort folder remove**: if the folder can't be
  deleted (file in use, etc.), the manifest still drops the
  reference. The orphan folder will be visible in the file manager
  but won't appear in the UI's snapshot history. A future
  "garbage collect orphan folders" pass could clean those up.
- **`SaveSlotViewModel.WireSnapshot` now wires three callbacks**:
  `OnRestoreRequested`, `OnEditTagRequested`, `OnDeleteRequested`.
  Each routes to `MainWindowViewModel` with `Slot` attached. Account
  variants are wired in `BackupCoordinator` but not yet surfaced in
  the UI (the account card doesn't have a snapshot history list
  yet).

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
../CubicOdysseyVault.UI/ViewModels/                 MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings / Onboarding / Snapshot / RestoreConfirm / TagEdit / DeleteConfirm VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml      toolbar + sidebar + slot WrapPanel + right detail panel
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml  modal config dialog
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml first-run wizard
../CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml restore confirm + game-running guard
../CubicOdysseyVault.UI/Views/TagEditDialog.axaml   tag editor modal
../CubicOdysseyVault.UI/Views/DeleteConfirmDialog.axaml delete confirm modal
../CubicOdysseyVault.Desktop/Program.cs             entry point
../CubicOdysseyVault.Tests/                         89 tests, all passing
```

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when sync first materializes the
  directory.
- **TGA RLE variant**: only uncompressed RGB observed.
- **Two-account-folder semantics (`0` vs `1`)**: only `0/` exists.
- **`pgrep -f CubicOdysseySteam` on Linux/Proton**: confirmed by
  reasoning, not by an actual run.
- **Windows process name**: assumed to literally be
  `CubicOdysseySteam`. Verify in Phase 9.

## Things deferred / open for later phases

- **Account-level snapshot UI**: the account card shows "Last
  backup" and a "Back up now" button but no history list. To
  surface tag/delete for account snapshots, add a history list to
  the account card or open a separate detail flyout.
- **Steam display name** from `localconfig.vdf`: still raw SteamID32.
- **Promote Cloud sources once `remote/` materializes**: needs a
  re-scan or a watcher on the `userdata/<id>/<app>/` parent.
- **Live "current state" health** (vs latest snapshot health).
- **Bulk operations**: select-many + "delete all auto > 30 days" or
  "tag-all". Building blocks exist; UI doesn't.
- **Garbage collect orphan snapshot folders**: if a `Directory.Delete`
  failed during `DeleteSnapshotEntry`, the folder is orphaned. A
  startup pass that removes folders not in any manifest would clean
  these up.
