# Handoff — pick up from Phase 5

> Updated 2026-05-06 at the end of the screenshot/polish phase.
> Read this first when resuming work.

## Where we are

**Phases 1–5 are done.** Solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 58 tests, and the desktop app shows real save
screenshots in slot cards + the right detail panel, with health
badges (green/yellow/red dots + label pills) derived from the most
recent snapshot, and color-coded trigger pills (Manual / Auto /
Pre-restore) on snapshot history rows.

What Phase 5 added:

- **`CubicOdysseyVault.Core/Tga/TgaDecoder.cs`** + `TgaImage` record:
  hand-rolled uncompressed-RGB TGA decoder. Handles 24/32 bpp,
  bottom-up + top-down origin, BGR→RGB swap, alpha passthrough on
  32 bpp / forced 0xFF on 24 bpp. RLE (`image_type 10`) intentionally
  rejected with `NotSupportedException` — flip on later if a save
  shows up using it. `TryDecodeFile` returns null on any error so the
  UI can fall through to a placeholder gracefully.
- **`CubicOdysseyVault.UI/Services/TgaBitmapLoader.cs`**: takes a
  `TgaImage` and produces an Avalonia `WriteableBitmap` in
  `Rgba8888` / `Unpremul`. Handles row-stride mismatch (`fb.RowBytes`
  may not equal `width*4`) with row-by-row copies.
- **`SaveSlotViewModel.Screenshot`** (`Bitmap?`): decoded once in the
  ctor from `screenshot.tga`. The VM is constructed on the discovery
  worker thread, which is fine — `WriteableBitmap` construction
  doesn't require the UI thread.
- **`SaveSlotViewModel` health flags**: `LatestHealth` (nullable
  `SlotHealth`), `LatestHealthLabel`, plus four bool flags
  (`IsHealthHealthy/Suspicious/Corrupted/Unchecked`) for `IsVisible`
  binding without a value converter. Re-fired on `Snapshots` change.
- **`SnapshotViewModel` trigger flags**: `IsTriggerManual/Auto/PreRestore`,
  plus a friendlier `TriggerLabel` (`"Pre-restore"` instead of the
  enum literal `"PreRestore"`).
- **`MainWindow.axaml`** updates:
  - Slot card grew to 240×232 to host a 92 px thumbnail (`Image
    Stretch="UniformToFill"` for 2.6:1 → 2.4:1 image ratio — minor
    crop) with a colored health-dot `Ellipse` overlaid in the
    top-right.
  - Detail panel got a hero screenshot
    (`Stretch="Uniform" MaxHeight="180"`) and a health pill next to
    the source label.
  - Snapshot history rows now lead with a colored trigger pill
    (Accent / HealthHealthy / HealthSuspicious for Manual / Auto /
    Pre-restore).
- **8 new TgaDecoder tests** (24/32 bpp, top-down + bottom-up,
  alpha forcing, RLE rejection, truncation, missing file). Total: 58.

## What's next: Phase 6 (file watcher + retention policy)

Per `docs/PLAN.md` item 6:

1. **`CubicOdysseyVault.Core/Watching/SaveWatcher.cs`** — one
   `FileSystemWatcher` per `SaveSource.RootPath`, recursive, filters
   `*.tmp`. Coalesces events into a per-slot debounce window
   (default 10 s, configurable via `AppSettings.WatcherDebounceSeconds`),
   then enqueues an Auto snapshot.
2. **`CubicOdysseyVault.Core/Snapshots/RetentionPolicy.cs`** —
   tiered/generational pruning. Defaults already wired in
   `AppSettings`: 24 hourly + 14 daily + 8 weekly auto, plus
   `Manual`/`PreRestore`/tagged forever. Run after each successful
   snapshot.
3. **`BackupService` integration** — call retention pruning after
   each snapshot is written. Auto trigger plus existing
   `Health == Corrupted` guard means a watcher event on a
   half-written file won't promote the corruption into the store.
4. **`MainWindowViewModel`** — own a `SaveWatcher` per discovered
   source, wire its enqueue callback to
   `BackupCoordinator.SnapshotSlotAsync(slot, Auto)`. Only run if
   `_settings.WatcherEnabled`. Tear down + recreate when settings
   change.
5. **UI**: status bar shows watcher state ("Watching N sources" /
   "Idle"); a small "auto" indicator on the slot card after an Auto
   snapshot lands.
6. **Tests**: `RetentionPolicyTests` with synthesized snapshot
   timelines (assert correct survivors after pruning); `SaveWatcher`
   harder to unit-test without flakiness — at minimum a debounce
   smoke test against a temp dir.

## Phase 5 design notes worth remembering

- **Why no IBrush converter**: I went with four `IsHealthXxx` bools +
  conditional `IsVisible` rather than a value converter, partly
  because compiled bindings prefer concrete types and partly because
  a converter adds an extra registered resource for one trivial
  enum→brush mapping. Same approach for `IsTriggerXxx` on snapshots.
  If a fourth or fifth state surfaces and the IsVisible-soup
  multiplies, refactor to a small `Converters/EnumToBrushConverter`.
- **Bitmap thread safety**: `WriteableBitmap` constructed on the
  discovery worker thread is fine — the lock/copy is local to that
  thread. Avalonia binds it to UI when the VM gets added to the
  main collection. If a future Phase needs to swap the bitmap
  reactively (e.g. lazy load on selection), do it on the UI thread.
- **TGA decoder is one-shot**: `Decode` allocates a new
  `byte[width*height*4]` per call. ~10 slots × 438 KB = ~4.4 MB
  allocated per refresh. Acceptable; the bytes are released as soon
  as `WriteableBitmap` finishes copying. No need to pool buffers.
- **Health = "latest snapshot's health"**: not "current slot's health"
  — computing live integrity on every refresh would mean ~50 MB
  read on this machine. Phase 6 may add a "Re-check now" button or
  hash on first selection.
- **Slot card thumbnail crop**: aspect ratio mismatch between
  240×92 (2.6:1) and 512×214 (2.4:1) means `UniformToFill` shaves a
  few pixels off left/right. Acceptable — the screenshot is
  context, not content. Detail panel uses `Uniform` so the full
  image is visible there.

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
../CubicOdysseyVault.Core/Snapshots/                Snapshot, SnapshotTrigger, SnapshotIndex, SnapshotStore, BackupService, BackupResult
../CubicOdysseyVault.Core/Tga/                      TgaImage, TgaDecoder
../CubicOdysseyVault.UI/Services/AppSettingsService.cs    JSON persistence + AppSettings record
../CubicOdysseyVault.UI/Services/BackupCoordinator.cs     async facade over BackupService
../CubicOdysseyVault.UI/Services/TgaBitmapLoader.cs       TgaImage → Avalonia WriteableBitmap
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml      palette
../CubicOdysseyVault.UI/ViewModels/                 MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings / Onboarding / Snapshot VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml      toolbar + sidebar + slot WrapPanel + right detail panel + thumbnails + health badges
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml  modal config dialog
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml first-run wizard
../CubicOdysseyVault.Desktop/Program.cs             entry point
../CubicOdysseyVault.Tests/                         58 tests, all passing
```

## Open assumptions still unvalidated

- **Steam Cloud `remote/` layout**: still inferred from
  `remotecache.vdf`. Validate when Cloud sync first materializes the
  `remote/` directory on this machine.
- **TGA RLE variant**: only uncompressed RGB observed
  (image_type 0x02). `TgaDecoder` rejects RLE with a clear error;
  add an RLE branch if image_type 0x0A surfaces.
- **Two-account-folder semantics**: only `0/` exists on this account.
  Plan enumerates dynamically.

## Things deferred from Phase 5 to later

- **Steam display name resolution** from
  `<root>/userdata/<SteamID32>/config/localconfig.vdf` — sidebar
  still shows raw `SteamID32`. Small VDF parsing job; can land
  alongside Phase 6 watcher work or as a standalone polish pass.
- **Live "current state" health** (vs. "latest snapshot health"):
  Phase 6 may add a "Re-check" action that runs `IntegrityChecker`
  on demand against the live slot.
- **Restore flow + game-running guard**: Phase 7.
- **Tag/rename/delete snapshots in UI**: Phase 8.
