# Handoff — pick up from Phase 6

> Updated 2026-05-06 at the end of the watcher/retention phase.
> Read this first when resuming work.

## Where we are

**Phases 1–6 are done.** The solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 77 tests, and the desktop app runs auto-snapshots
in the background as save files change, prunes old auto snapshots
according to a tiered/generational retention policy, and keeps
manual + tagged + pre-restore snapshots forever.

What Phase 6 added:

- **`CubicOdysseyVault.Core/Snapshots/RetentionPolicy.cs`** — pure
  function from a list of `Snapshot`s + a `Settings` record (defaults
  24 hourly / 14 daily / 8 weekly) + a `nowUtc` to a `(Keep, Prune)`
  plan. Algorithm: always keep `Manual`, `PreRestore`, and any
  snapshot with a non-empty `Tag`; for the rest (Auto, untagged),
  bucket each into hourly / daily / weekly tiers based on its UTC
  age, keeping one per bucket per tier and dropping anything beyond
  the weekly window.
- **`CubicOdysseyVault.Core/Watching/SaveWatcher.cs` + `SlotKey.cs`** —
  one `FileSystemWatcher` per `SaveSource.RootPath`, recursive,
  filters `*.tmp`. Per-key debounce timers (one per `SlotKey`, one
  per account-level SteamID32) coalesce rapid event bursts into a
  single fire after the configured quiet window. Path classification
  is exposed `internal static` for unit testing without touching
  the filesystem.
- **`BackupService` integration** — every successful snapshot is
  followed by `RetentionPolicy.Apply` against the slot/account's
  manifest; pruned folders are best-effort deleted; manifest is
  rewritten with only the kept entries (then atomically saved as
  before). `BackupService` ctor now takes an optional
  `RetentionPolicy.Settings`; falls back to `Default` if null. Auto
  triggers still abort on `Corrupted` slots so an interrupted
  in-place write never gets promoted into the store.
- **`SaveSlotViewModel.BackUpAsync(SnapshotTrigger)`** + parallel
  method on `SaveAccountViewModel`: the `BackUpNow` `[RelayCommand]`
  delegates to `BackUpAsync(Manual)` so the existing button keeps
  working; the watcher path calls `BackUpAsync(Auto)` directly.
  `BackupRequested` callback signature gained a trigger parameter.
  Status text differentiates "Saved at HH:mm:ss" (manual) from
  "Auto-saved at HH:mm:ss" (auto) for inline feedback.
- **`MainWindowViewModel`** — owns a `List<SaveWatcher>`, starts them
  after each successful `RefreshDiscoveryAsync` (skipping sources
  with `Exists=false` or non-existent paths), tears them down at the
  start of each refresh + on settings change. Watcher callbacks
  marshal to the UI thread via `Dispatcher.UIThread.Post` and
  resolve the matching VM by `(SteamID, accountFolder, slotName,
  Source.Kind, Source.RootPath)`. Status bar appends "Watching N
  sources." or "Watcher idle." after the discovery summary.
- **`BackupCoordinator.Update(backupRoot, retention)`** replaces the
  earlier `UpdateBackupRoot(string)`. Called from
  `MainWindowViewModel.ApplySettings` so retention numbers entered
  in the Settings dialog take effect on the next snapshot.
- **Tests** — 9 new for `RetentionPolicy` (Manual/PreRestore/tagged
  always kept; per-bucket dedup at hourly/daily/weekly tiers; ancient
  Auto pruned); 10 new for `SaveWatcher` (path classification +
  two FSW integration tests with real temp dirs and 150 ms debounce).
  Total: 77.

## What's next: Phase 7 (restore + pre-restore snapshot + game-running guard)

Per `docs/PLAN.md` item 7:

1. **`CubicOdysseyVault.Core/Restore/RestoreService.cs`** —
   `RestoreAsync(slot, snapshot)` orchestrates:
   1. Confirm dialog with snapshot screenshot + timestamp + tag (UI).
   2. Detect whether `CubicOdysseySteam.exe` is running (`pgrep` on
      Linux, `Process.GetProcessesByName` on Windows). Block restore
      with a clear message if it is. Constants already has
      `CubicOdysseyProcessName` for this.
   3. Take a `PreRestore`-trigger snapshot of the current slot state
      so the restore is itself undoable. PreRestore is in the
      retention "always keep" bucket.
   4. Stage snapshot files into a sibling `*.restore-tmp/` folder.
   5. Atomic swap: rename live slot → `*.replaced-<timestamp>/`,
      rename `*.restore-tmp/` → live slot folder.
   6. Verify the restored slot passes `IntegrityChecker`.
   7. Delete `*.replaced-*` after a configurable cooldown (default:
      keep one generation).
2. **UI**:
   - Per-snapshot "Restore" button in the right detail panel.
   - `RestoreConfirmDialog` showing the snapshot's screenshot,
     timestamp, file count, total bytes, tag, and the source kind
     it'll restore into. Two buttons: "Restore" / "Cancel".
   - Status bar feedback during restore.
   - Hard refusal dialog ("Close Cubic Odyssey first") if the
     game-running check fires.
3. **Tests**:
   - `RestoreServiceTests` against synthetic snapshot folders
     (skip the process-running check via injected predicate).

## Phase 6 design notes worth remembering

- **Retention boundaries are relative to `nowUtc`** — `RetentionPolicy.Apply`
  takes the current time as a parameter rather than reading
  `DateTime.UtcNow` internally. Makes tests deterministic. `BackupService`
  passes `DateTime.UtcNow` at the call site.
- **Bucket truncation** is UTC-anchored: hour bucket = floor to the
  hour; day bucket = floor to midnight UTC; week bucket = previous
  Sunday 00:00 UTC. Local-time anchoring would shift bucket
  boundaries depending on the user's TZ — undesirable for a backup
  tool.
- **Watcher debounce is per-key**, not global. If slots 1 and 5 both
  receive events at the same instant, two independent timers run.
  Without per-key debounce, a busy slot could starve a quiet one.
- **Path classification accepts both `/` and `\\`** — FSW always uses
  one or the other depending on platform, but the parser is strict
  about which without the cross-platform fix; consumers may pass
  either form synthetically.
- **Sources with `Exists=false` are skipped at watcher start** — the
  Steam Cloud `remote/` source comes back from `SaveLocator` even
  when the directory hasn't materialized yet; FSW would throw on
  ctor for a missing path. Phase 8 or later may add a re-scan loop
  that promotes such sources once they appear.
- **Watcher events fire on thread-pool threads** — `MainWindowViewModel`
  marshals to UI thread via `Dispatcher.UIThread.Post` before
  invoking `BackUpAsync`, since the latter touches
  `ObservableCollection`/`[ObservableProperty]` state.
- **No double-fire guard for fast watcher restarts**: stopping +
  restarting watchers (e.g. on settings save) drains the FSW's queue
  but doesn't cancel in-flight debounce timers. In practice, harmless
  — the worst case is a single Auto snapshot fires after a settings
  change, which is the same as if the user had let the timer run.

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
../CubicOdysseyVault.UI/Services/                   AppSettingsService, BackupCoordinator, TgaBitmapLoader
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml      palette
../CubicOdysseyVault.UI/ViewModels/                 MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings / Onboarding / Snapshot VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml      toolbar + sidebar + slot WrapPanel + right detail panel
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml  modal config dialog
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml first-run wizard
../CubicOdysseyVault.Desktop/Program.cs             entry point
../CubicOdysseyVault.Tests/                         77 tests, all passing
```

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when Cloud sync first materializes the
  directory on this machine.
- **TGA RLE variant**: only uncompressed RGB observed. `TgaDecoder`
  rejects RLE explicitly; add an RLE branch if a save uses
  image_type 0x0A.
- **Two-account-folder semantics (`0` vs `1`)**: only `0/` exists on
  this account. Plan enumerates dynamically.
- **Game-running detection on Linux/Proton**: Phase 7's check needs
  to handle `wine` / `wineserver` processes hosting
  `CubicOdysseySteam.exe`, not just a literal process named that.
  `pgrep -f CubicOdysseySteam` is one option to test.

## Things deferred from Phase 6

- **Live "current state" health** (vs. "latest snapshot health"):
  could surface as a "Re-check" button. Not blocking any later
  phase.
- **Steam display name resolution** from `localconfig.vdf`: still
  not done; sidebar shows raw `SteamID32`.
- **Promote Steam-Cloud sources once `remote/` appears**: requires
  a periodic re-scan or a watcher on the parent directory. Phase 8+.
