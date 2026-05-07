# Handoff — pick up from Phase 4

> Updated 2026-05-06 at the end of the manual-snapshot phase.
> Read this first when resuming work.

## Where we are

**Phases 1 (Skeleton), 2 (Discovery), 3 (Onboarding/settings), and
4 (Manual snapshot + integrity + snapshot store) are done.** The
solution builds clean (`dotnet build CubicOdysseyVault.sln` →
0 warnings, 0 errors), `dotnet test` passes 50 tests, and the desktop
app supports end-to-end manual snapshots with skip-if-unchanged
semantics, integrity checking, and per-slot snapshot history.

What Phase 4 added:

- **`CubicOdysseyVault.Core/Integrity/`**:
  - `SlotHealth` enum (Healthy / Suspicious / Corrupted).
  - `IntegrityFileResult`, `IntegrityReport` records.
  - `IntegrityChecker.InspectSlot(SaveSlot)` and `InspectAccount(SaveAccount)`
    do a single-pass SHA-256 + NULL-block scan + TGA header validation
    (image_type 2/10/3/11 accepted). Combined hash is a deterministic
    SHA-256 over `<filename>:<file_hash>\n` lines (sorted by filename),
    so reordering or rediscovering the same files yields the same hash —
    the foundation of skip-if-unchanged.
- **`CubicOdysseyVault.Core/Snapshots/`**:
  - `SnapshotTrigger` (Manual / Auto / PreRestore), `Snapshot` POCO
    (class with public properties so System.Text.Json round-trips
    cleanly), `SnapshotManifest` wrapper.
  - `SnapshotIndex` — JSON load/save with `*.tmp` + `File.Move(overwrite)`
    atomic-rewrite pattern.
  - `SnapshotStore` — folder layout helpers + `CopyFilesAtomically`
    (write each file to `*.tmp`, rename to final name).
  - `BackupResult` — success/skipped/snapshot/reason quad.
  - `BackupService` — `SnapshotSlot` and `SnapshotAccount` orchestrate
    integrity → skip-if-unchanged → atomic copy → manifest update.
    Auto trigger aborts on Corrupted; Manual trigger goes through
    (user explicitly chose to back up).
- **`CubicOdysseyVault.UI/Services/BackupCoordinator.cs`** — async
  facade over `BackupService`; recreates the inner service when the
  backup root changes via settings.
- **`CubicOdysseyVault.UI/ViewModels/`**:
  - `SnapshotViewModel` — display wrapper for a `Snapshot`.
  - `SaveSlotViewModel` / `SaveAccountViewModel` — gain
    `BackUpNowCommand`, `IsBackingUp`, `Snapshots`
    `ObservableCollection<SnapshotViewModel>`, `BackupStatus` for
    inline feedback, `LastSnapshotText` derived for the slot card.
  - `MainWindowViewModel` — owns a `BackupCoordinator`; tracks
    `SelectedSlot` for the right detail panel; `DiscoverSync` now
    wires each slot/account VM's `BackupRequested` callback and
    pre-loads existing snapshots from the manifest so the history
    is populated on launch.
- **`CubicOdysseyVault.UI/Views/MainWindow.axaml`** — added a right
  detail panel (340 px) that becomes visible when a slot is selected;
  shows slot metadata, "Back up now" button, status line, and a
  vertical snapshot history list. Slot grid moved from `ItemsControl`
  to `ListBox` so `SelectedSlot` binding works. Each slot card now
  has its own "Back up now" button. Account-level card has a
  "Back up now" button + "Last backup: …" inline text.
- **Tests** — 20 new tests across `IntegrityCheckerTests` (8),
  `SnapshotIndexTests` (5), and `BackupServiceTests` (7). Total 50.

## What's next: Phase 5 (TGA decoder + slot UI polish)

Per `docs/PLAN.md` item 5:

1. **`Tga/TgaDecoder.cs`** in `CubicOdysseyVault.Core/Tga/` — minimal
   uncompressed-TGA → byte[] RGBA decoder. Real saves on this
   machine use image_type 2 (uncompressed RGB), so the
   uncompressed-only path is enough for now. Add an RLE branch only
   if image_type 10 turns up later.
2. **Slot card thumbnails** — replace the placeholder `Rectangle` in
   the slot card with an `<Image>` whose source is decoded from
   `screenshot.tga`. Lazy-load on slot enumeration; cache decoded
   bitmaps per slot so refreshes don't re-decode.
3. **Slot card health badge** — small colored dot using the
   `HealthHealthy` / `HealthSuspicious` / `HealthCorrupted` brushes
   from `DarkTheme.axaml`. Health derived from the most recent
   snapshot in the manifest; "Unchecked" when no snapshots exist
   (gray dot).
4. **Detail panel screenshot** — full-size screenshot at the top of
   the right detail panel.
5. **Polish on snapshot history rows** — trigger badge color
   (Manual/Auto/PreRestore), hover states, tag display.

Optional in Phase 5 if time permits, otherwise Phase 6:

6. Steam display name resolution from
   `<steam-root>/userdata/<SteamID32>/config/localconfig.vdf`. Sidebar
   currently shows the raw `SteamID32`.

## Phase 4 design notes worth remembering

- **Combined hash determinism**: The combined slot hash is the
  SHA-256 of `<name>:<filehash>\n` lines sorted by filename. This is
  what skip-if-unchanged compares against — `BackupService` reads the
  manifest's most recent snapshot and bails out if its
  `CombinedHash` matches the current one. Without the deterministic
  ordering, the watcher in Phase 6 would create a snapshot on every
  enumeration even when nothing changed.
- **Auto vs Manual on Corrupted**: `BackupService.SnapshotSlot`
  rejects an Auto trigger against a Corrupted slot
  (`null` files = interrupted in-place write). Manual triggers are
  allowed through — the user explicitly chose to capture even a
  damaged state, and might want it for forensics. Same rule applies
  to `SnapshotAccount`. Tests: `SnapshotSlot_AutoTriggerOnCorruptedSlot_Aborts`
  vs `SnapshotSlot_ManualTriggerOnCorruptedSlot_StillAllows`.
- **Atomic file ops**: `SnapshotStore.CopyFilesAtomically` writes each
  file as `<name>.tmp` and `File.Move(overwrite: true)`s into place.
  `SnapshotIndex.Save` does the same for the manifest. POSIX
  guarantees atomicity; Windows uses MoveFileEx replace semantics.
  Either way, an interrupted snapshot leaves the previous good state
  intact (worst case: a `.tmp` file lying around, which the next
  copy will overwrite).
- **Snapshot folder name format**: `<UTC ISO8601 with hyphens>__<6-hex>`
  e.g. `2026-05-07T00-00-47Z__f50583`. ISO8601 with `:` is invalid in
  Windows filenames, hence the dashes. The 6-hex suffix comes from
  the slot's combined hash; it's both human-readable
  ("did anything change?") and disambiguates same-second snapshots.
- **Account-level snapshots live under `<backupRoot>/snapshots/<SteamID32>/_account/`**;
  slot snapshots under `<backupRoot>/snapshots/<SteamID32>/<accountFolder>/<slot>/`.
  `_account` is a sentinel name that can't collide with a real
  account folder (game uses `0`/`1`).
- **`BackupCoordinator.UpdateBackupRoot`** is called from
  `MainWindowViewModel.ApplySettings` whenever the user changes the
  backup root via the Settings dialog. Inner `BackupService` is
  swapped wholesale (cheap; no shared state to migrate).

## Style template — non-negotiable (unchanged)

All Avalonia code in this project must mirror the patterns in
`/run/media/james/SSD/My_Programs_SSD/OpenFATX/`:

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
PLAN.md                                           full design spec (this dir)
HANDOFF.md                                         this file
../Directory.Build.props                           net8.0 + Avalonia 11.2.3
../CubicOdysseyVault.Core/Constants.cs             AppId + path constants + candidate-roots tables
../CubicOdysseyVault.Core/Steam/                   SteamRoot, SteamLocator, LibraryFoldersVdfParser
../CubicOdysseyVault.Core/Saves/                   SaveSource, SaveAccount, SaveSlot, SaveLayout, SaveLocator, SaveSlotEnumerator
../CubicOdysseyVault.Core/Integrity/               SlotHealth, IntegrityChecker, IntegrityReport
../CubicOdysseyVault.Core/Snapshots/               Snapshot, SnapshotTrigger, SnapshotIndex, SnapshotStore, BackupService, BackupResult
../CubicOdysseyVault.UI/Services/AppSettingsService.cs   JSON persistence + AppSettings record
../CubicOdysseyVault.UI/Services/BackupCoordinator.cs    async facade over BackupService
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml     palette
../CubicOdysseyVault.UI/ViewModels/                MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings / Onboarding / Snapshot VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml     toolbar + sidebar + slot WrapPanel + right detail panel
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml modal config dialog
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml first-run wizard
../CubicOdysseyVault.Desktop/Program.cs            entry point
../CubicOdysseyVault.Tests/                        50 tests, all passing
```

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when Cloud sync first materializes the
  `remote/` directory on this machine.
- **TGA RLE variant**: only uncompressed RGB observed
  (image_type 0x02). Phase 5's TGA decoder starts uncompressed-only;
  add an RLE branch if image_type 0x0A surfaces.
- **Two-account-folder semantics**: only `0/` exists on this account.
  Plan enumerates dynamically.
- **Slot file naming variants**: snapshot pipeline captures every
  file in a slot folder verbatim — no name filter — so the captured
  slot will round-trip even if the game adds new file types later.

## Things Phase 4 deliberately deferred

- **Live integrity check on enumeration**: would require reading
  every byte of every slot on every refresh (~50 MB on this
  machine). Currently health is only known after a snapshot has been
  taken. Phase 6 may hash on-demand when the user selects a slot.
- **Health badge on slot cards**: same reason — surfacing health
  without computing it is misleading. Comes in Phase 5 via "health
  of latest snapshot" or in Phase 6 via lazy computation.
- **Restore flow + game-running guard**: Phase 7.
- **Auto-snapshot via FileSystemWatcher + retention pruning**: Phase 6.
- **Tag/rename/delete snapshots in UI**: Phase 8.
