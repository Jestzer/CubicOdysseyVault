# Handoff — all PLAN.md phases shipped + new-work Phase 1 shipped

> Updated 2026-05-07 after the save inspector + per-item icons + game
> install setting work. See `README.md` for the user-facing description;
> this file is for the next person picking up follow-up work. The big
> open item is **new-work Phase 2 (map viewer)** — see its dedicated
> section below.

## Where we are

**All 10 phases of `docs/PLAN.md` plus Phase 1 of the new work are
done.** The solution builds clean
(`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 141 tests on Linux, and the desktop app supports
the full backup pipeline (discover → snapshot → restore → tag/delete)
plus a save inspector with a digestable Summary tab showing
character, timestamp, inventory containers with category badges,
per-item atlas icons, and ships.

What new-work Phase 1 added (after Phase 10 of PLAN.md):

- **App icon** — hand-authored isometric voxel-cube SVG at
  `assets/icon.svg` rendered to PNGs (Inkscape) + multi-size
  `.ico` (ImageMagick). Wired as `Icon=` on every dialog and as
  `<ApplicationIcon>` for the Desktop project. Cube fills ~85%
  of the canvas so it reads at 16 px.
- **TGA orientation fix** — Cubic Odyssey writes screenshots
  top-down despite leaving the descriptor's origin bit clear
  (which spec-says bottom-up). `TgaDecoder` now ignores the bit
  and always reads top-down.
- **Save inspector** with three tabs (Decoded / Strings / Hex)
  per file, anchored by a top-level Summary tab.
  - `Core/SaveContent/SaveBlobReader` strips a u32 length prefix
    and decompresses the zstd frame (via `ZstdSharp.Port` —
    first non-framework dep, justified because zstd is standard
    and not reasonable to hand-roll).
  - `Core/SaveContent/TlvParser` walks the `[u32 header][u16 count]`
    + `[u16 tag][u16 type][u32 length][bytes]` structure used by
    `.sav` payloads. Type 23 (0x17) recurses for nested lists.
  - `Core/SaveContent/{StringsExtractor, HexFormatter, KnownTagAnnotations}`.
  - `Core/SaveContent/InventoryExtractor` heuristically pulls
    items + counts from `93_client_state.sav` by scanning for
    `[tag=1 type=12 string identifier][tag=2 float32 dur]
    [tag=3 int32 count]` records, grouped by literal `inventory`
    / `__quickslots` fence-post strings.
  - `Core/SaveContent/{ItemMetadata, ItemConfigParser, ItemCatalog}`
    parse the game's per-item `.cfg` files at
    `<install>/data/configs/items/*.cfg` (1346 items in sample)
    for friendly titles + tier + type.
  - `Core/SaveContent/SpriteAtlas` parses `<install>/data/sprites/icons.bspr`
    (BSPR magic + 12-byte records `[u32 reserved][u16 x][u16 y][u16 w][u16 h]`
    from offset 8) and pairs it with `icons.png` for per-item
    atlas icons.
  - `Core/SaveContent/SaveSummary` + `SaveSummaryBuilder` compose
    the slot's character name, timestamp, inventory containers,
    and ship list.
- **`AppSettings.GameInstallPath`** — Browse-able TextBox in
  Settings; empty = auto-discover under each Steam root's
  `steamapps/common/Cubic Odyssey/`. `MainWindowViewModel.EnsureCatalog`
  prefers the override and drops the cached catalog when the
  setting changes.
- **UI** — new `SaveInspectorDialog` modal with file list + tab
  control; new `SaveSummaryViewModel`,
  `InventoryContainerViewModel`, `InventoryItemViewModel`,
  `TlvEntryViewModel`. Each inventory row leads with a 40×40
  atlas icon (when `inv_frame` resolves) plus a colored category
  badge fallback.
- **`UI/Services/IconAtlasCache`** — process-wide cache for the
  decoded atlas Bitmap; per-item slices are zero-allocation
  `CroppedBitmap` projections.
- **Tests** — Phase 1 added 52 tests across SaveContent (parser,
  extractor, catalog, atlas) + a TGA orientation regression. Total: 141.

What Phase 9 (PLAN.md) added:

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

## New-work Phase 2 — Map viewer (DEFERRED, the big open item)

The original ask: a viewer for the user's voxel saves. Phase 1 was
"the easy targets" (item lists, character, timestamp, screenshots);
Phase 2 is rendering the actual ship designs and world chunks the
player has built or explored.

**Goal**

Show ship `.vx` files as rotatable 3D-ish previews (or at minimum a
rendered-once isometric thumbnail per ship, displayed in the Summary
tab next to the filename), and ideally render `.vw3` world chunks
into a navigable map view. Even just successfully decoding a single
ship's voxel grid and rendering it as an isometric image would be a
big win.

### Data formats

**`ship_*.vx`** — plain-text-header binvox-like format. Confirmed
shape from `ship_1.vx`:

```
#binvox 3
dim 56 56 56
translate 0 0 0
scale 1
data
<voxel byte stream>
```

The header lines are exactly 5 lines, each terminated by `\n`. After
`data\n`, the rest of the file is voxel data. Empirically the first
byte of the data section is `\0` and there are runs of `\0\0\0\0 \xff\0\0\0`
in the early bytes. **Format unknown** — almost certainly RLE-encoded
but the unit size needs work. Standard binvox v1/v2 uses 1-byte (value,
count) pairs; CO's `binvox 3` may have a wider value (for material
indices) or different layout. Worth comparing against
[`patrickmin/binvox`](https://www.patrickmin.com/binvox/binvox.html)
to see how far the upstream format diverges.

**`93_*.vw3`** — per-slot world chunks. Real samples on this user's
machine range from ~28 KB to ~1.2 MB. The first 20 bytes are an
opaque header; **byte 20 is the start of a zstd frame** (`28 b5 2f fd`
magic). Decompressing the small one (28 KB → 6.7 KB) yields what
look like uniformly-sized records of voxel positions + content; the
exact record size still has to be worked out. Try common record sizes
(8, 12, 16 bytes); look for 3-byte or 4-byte position triples
followed by a small block-type word. Also check whether the 20-byte
header has decompressed-size or chunk-coord fields the rendering
code can use.

**Block types** referenced from save strings: `res.voxel.leaves_7`,
`res.voxel.trunk`, `res.voxel.trunk_3`, `res.voxel.rock_3`,
`res.voxel.dirt_savana_base`, `res.voxel.dirt_tropical_base`. The
game's `data/configs/items/` folder has a corresponding `.cfg` for
each — those will give friendly names + colors for legend rendering.
There's likely also a separate block-id-to-identifier table somewhere
in the `data/` tree; worth grepping for `res.voxel.` in `data/configs/`
during investigation.

### Open questions to resolve early

- What's the unit size + structure of binvox-3 RLE? (decode `ship_1.vx`
  to a 56³ voxel grid and verify by rendering — if the silhouette
  looks like a ship, the format is correct).
- What's in the 20-byte `.vw3` header? (chunk coords? decompressed
  size? a hash?)
- What's the record layout in the decompressed world chunk? (likely
  position + block id; verify by looking for sequential coordinates).
- Where does the block-id → identifier mapping live? (probably in
  one of the .cfg files or a separate index — try grepping
  `data/` for `res.voxel`).

### Suggested architecture

Voxel rendering doesn't need a 3D engine. Avalonia's 2D Skia surface
is enough for an isometric projection — the same trick the app icon
uses. Sort voxels back-to-front and draw three quads per visible
voxel (top + two side faces). At 56³ that's 175k max voxels but most
are empty; with sparse traversal a few thousand visible faces is
typical, well within Skia's budget.

Files I'd add (Core stays Avalonia-free; rendering is UI):

```
CubicOdysseyVault.Core/Voxels/
  BinvoxV3Reader.cs        — parse #binvox 3 header + RLE data into a 3D grid + dim
  WorldChunkReader.cs      — strip 20-byte header, decompress, parse into voxel-positions
  VoxelGrid.cs             — IReadOnlyList<(x, y, z, type)> + bounds, used by the renderer
  BlockTypeCatalog.cs      — block-id → identifier + color, loaded alongside ItemCatalog
CubicOdysseyVault.UI/Services/
  VoxelRenderer.cs         — isometric Bitmap render of a VoxelGrid (single bitmap, cached)
CubicOdysseyVault.UI/ViewModels/
  ShipPreviewViewModel.cs  — wraps VoxelGrid + a render-on-demand Bitmap
  WorldChunkViewModel.cs   — same but for .vw3 chunks
CubicOdysseyVault.UI/Views/
  MapViewerDialog.axaml    — modal with a list of chunks/ships + the rendered preview
                             (or fold the ship thumbnails into the existing Summary tab
                             alongside the ship filename list)
CubicOdysseyVault.Tests/
  BinvoxV3ReaderTests.cs   — synthetic input fixtures once the format is nailed down
  WorldChunkReaderTests.cs — same
  VoxelRendererTests.cs    — golden-image-ish (compare a tiny grid render to known output)
```

Two phases of integration:

1. **Ship thumbnails** in the Summary tab next to each `ship_*.vx`
   filename — a small (~80×80) isometric render per ship. This gives
   immediate visual feedback in the existing UI without a new dialog.
2. **Full map viewer** as a separate modal that loads all `.vw3`
   chunks for a slot, lets the user zoom/pan, and optionally toggle
   block types in a legend. Bigger lift; do after ships work.

### Verification

- `BinvoxV3Reader` test: synthesize a 4×4×4 grid with a known shape
  (e.g. solid cube, hollow cube, single-voxel) and assert the
  decoded VoxelGrid matches.
- Real-world smoke: render `ship_1.vx` to a PNG and eyeball it
  against an in-game screenshot.
- For `.vw3`: decompress the smallest sample (28 KB) and walk the
  records; check that decoded positions stay within plausible bounds
  (e.g., 0..255 per axis).

### Things to read before starting

- Patrick Min's binvox spec — primary reference, even if CO's v3
  diverges.
- `ItemCatalog.LoadFrom` and `SpriteAtlas.LoadFromGameInstall`
  patterns — the new readers should follow the same shape (load
  from game install dir, return null/empty when files missing).
- `TgaBitmapLoader` — pattern for "Core decoder produces raw bytes,
  UI service wraps them in an Avalonia Bitmap". Voxel renderer
  should follow the same split (Core math, UI bitmap).

### Why not just use a 3D engine

OpenTK/Silk.NET would work but adds a hefty native dep + a much
bigger learning curve to maintain. Avalonia 11.x doesn't ship 3D.
For a static voxel preview the iso-projection path is small,
testable, and matches the existing Skia rendering surface — and the
result is also automatically saveable as a PNG (which is handy for
comparison snapshots). If at some point we want free-rotation,
revisit then.

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
- **`.str` localization files** — `.cfg` `title_string` values like
  `STR_SUIT_2` aren't resolved to their localized text yet
  ("Suit Mk II"). The save inspector falls back to a humanized
  identifier ("Suit (Tier 2)") which reads cleanly. Resolving needs
  a `.str` parser; CO's format is undocumented.
- **`icons.bspr` trailing index table** — the `(u16 frame_id, u16 count)`
  pairs after the rect records are likely animation grouping
  metadata. Not needed for static icon lookup, but if we ever want
  animated icons or want to validate inv_frame ranges, this is
  where to start.

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
PLAN.md                                              full design spec (this dir)
HANDOFF.md                                            this file
../assets/icon.svg                                    source-of-truth icon SVG
../Directory.Build.props                              net8.0 + Avalonia 11.2.3
../CubicOdysseyVault.Core/Constants.cs                AppId + path constants + candidate-roots tables
../CubicOdysseyVault.Core/Steam/                      SteamRoot, SteamLocator, LibraryFoldersVdfParser
../CubicOdysseyVault.Core/Saves/                      SaveSource, SaveAccount, SaveSlot, SaveLayout, SaveLocator, SaveSlotEnumerator
../CubicOdysseyVault.Core/Integrity/                  SlotHealth, IntegrityChecker, IntegrityReport
../CubicOdysseyVault.Core/Snapshots/                  Snapshot, SnapshotTrigger, SnapshotIndex, SnapshotStore, RetentionPolicy, BackupService, BackupResult
../CubicOdysseyVault.Core/Tga/                        TgaImage, TgaDecoder
../CubicOdysseyVault.Core/Watching/                   SlotKey, SaveWatcher
../CubicOdysseyVault.Core/Restore/                    RestoreResult, GameProcessDetector, RestoreService
../CubicOdysseyVault.Core/SaveContent/                SaveBlobReader, TlvParser, StringsExtractor, HexFormatter, KnownTagAnnotations,
                                                       ItemMetadata, ItemConfigParser, ItemCatalog, SpriteAtlas,
                                                       InventoryItem, InventoryExtractor, SaveSummary, SaveSummaryBuilder
../CubicOdysseyVault.UI/Services/                     AppSettingsService, BackupCoordinator, TgaBitmapLoader, IconAtlasCache
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml        palette + category colors
../CubicOdysseyVault.UI/ViewModels/                   MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource / Settings /
                                                       Onboarding / Snapshot / RestoreConfirm / TagEdit / DeleteConfirm /
                                                       SaveInspector / SaveFile / TlvEntry / SaveSummary /
                                                       InventoryContainer / InventoryItem VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml        toolbar + sidebar + slot WrapPanel + right detail panel
../CubicOdysseyVault.UI/Views/SaveInspectorDialog.axaml save inspector with Summary + Files tabs
../CubicOdysseyVault.UI/Views/SettingsDialog.axaml    modal config dialog (incl. game install path)
../CubicOdysseyVault.UI/Views/OnboardingDialog.axaml  first-run wizard
../CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml restore confirm + game-running guard
../CubicOdysseyVault.UI/Views/TagEditDialog.axaml     tag editor modal
../CubicOdysseyVault.UI/Views/DeleteConfirmDialog.axaml delete confirm modal
../CubicOdysseyVault.Desktop/Program.cs               entry point
../CubicOdysseyVault.Tests/                           141 tests, all passing
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

- **New-work Phase 2 — Map viewer**: full briefing in its own
  section above. The big remaining feature.
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
