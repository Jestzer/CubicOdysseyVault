# Handoff — pick up from Phase 8

> Updated 2026-05-06 at the end of the tag/delete phase.
> Read this first when resuming work.

## Where we are

**Phases 1–8 are done.** The solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 89 tests, and the desktop app supports the
full snapshot lifecycle: discover → snapshot (manual + auto) →
browse history → restore → tag → delete.

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

## What's next: Phase 9 (cross-platform polish)

Per `docs/PLAN.md` item 9:

1. **Windows Documents path resolution** — replace the
   `// TODO Phase 9` stub in `SaveLocator.AddDocumentsSource` with a
   real implementation. Need to handle:
   - The standard Documents folder via
     `Environment.GetFolderPath(SpecialFolder.MyDocuments)` or
     `SHGetKnownFolderPath(FOLDERID_Documents)` (the latter is the
     only correct way under OneDrive redirection).
   - OneDrive Documents — typically at
     `%USERPROFILE%\OneDrive\Documents\` but the registered path is
     in `HKCU\Software\Microsoft\OneDrive\UserFolder` and
     `KnownFolders` may or may not point there depending on how
     OneDrive set up Backup-PC. Better: `SHGetKnownFolderPath`
     resolves through the redirect automatically.
2. **Windows registry probe for the Steam install path** —
   `SteamLocator.ReadWindowsRegistryPaths` currently returns
   `Array.Empty<string>()`. Add `HKCU\Software\Valve\Steam\SteamPath`
   via `Microsoft.Win32.Registry` (gated by `OperatingSystem.IsWindows()`).
3. **Build + smoke-test on Windows** — at minimum, verify the
   solution builds in a Windows .NET 8 SDK, the app launches, and
   discovery picks up Steam from the registry + `libraryfolders.vdf`
   parses correctly. Easiest path: a Windows VM, or GitHub Actions
   `windows-latest` runner.
4. **Cross-platform `pgrep` fallback** — Windows uses
   `Process.GetProcessesByName("CubicOdysseySteam")` which only
   matches if the process name is literally that. If Cubic Odyssey
   on Windows runs as `CubicOdysseySteam.exe`, this works; otherwise
   add a fallback. (Linux/Proton already shells out to `pgrep -f`.)

## What's next: Phase 10 (README + screenshots)

After cross-platform polish lands:

1. README describing what the tool does and how to run it.
2. A handful of screenshots: main window, settings dialog,
   onboarding wizard, restore confirm.
3. Optional: a brief design notes section pointing at the manifest
   format and snapshot layout for users who want to understand the
   on-disk shape.

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
