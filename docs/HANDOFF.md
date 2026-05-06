# Handoff — pick up from Phase 2

> Updated 2026-05-06 at the end of the discovery phase, after the user
> switched to a Linux account with real Cubic Odyssey saves and grounded
> the implementation in actual data. Read this first when resuming work.

## Where we are

**Phases 1 (Skeleton) and 2 (Discovery) are done.** The solution builds
clean (`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors),
`dotnet test` passes 25 tests, and `dotnet run --project
CubicOdysseyVault.Desktop` opens the dark window, auto-scans on first
attach, and lists discovered Steam users + slot cards.

What Phase 2 added:

- **Core models** (`CubicOdysseyVault.Core`):
  - `Steam/SteamRoot.cs`, `Steam/SteamLocator.cs` — cross-platform Steam
    install discovery; resolves intermediate symlinks via segment-walk
    (multi-iteration; .NET's `ResolveLinkTarget` only handles leaf links).
  - `Steam/LibraryFoldersVdfParser.cs` — hand-rolled tokenizer for Valve
    KeyValues format, no NuGet dependency.
  - `Saves/SaveSource.cs`, `Saves/SaveAccount.cs`, `Saves/SaveSlot.cs`,
    `Saves/SaveLayout.cs` — flat records, no Avalonia refs.
  - `Saves/SaveLocator.cs` — produces `ProtonCompatdata` /
    `SteamCloudRemote` / `Documents` (Win stub) / `Manual` sources;
    dedupes by canonical `RootPath`.
  - `Saves/SaveSlotEnumerator.cs` — walks `<RootPath>/<SteamID32>/<acct>/<slot>/`,
    surfaces both `SaveAccount` (account-level shared files) and
    `SaveSlot` (per-slot atomic units).
- **UI ViewModels** (`CubicOdysseyVault.UI/ViewModels`):
  `SteamUserViewModel`, `SaveAccountViewModel`, `SaveSlotViewModel`,
  `SaveSourceViewModel`, plus `MainWindowViewModel` extended with
  `RefreshDiscoveryCommand` and `ShowEmptyState`.
- **MainWindow.axaml** — sidebar `ListBox` of users, content
  `ItemsControl` for the account-level card + `WrapPanel` of slot cards.
- **MainWindow.axaml.cs** — auto-fires `RefreshDiscoveryCommand` once on
  first `DataContextChanged` per user decision (auto-scan-on-launch).
- **Tests** — `LibraryFoldersVdfParserTests` (11), `SaveSlotEnumeratorTests`
  (8), `SaveLocatorTests` (5), plus the original `ConstantsTests`.

## Important discoveries from real data

1. **Account-level shared files** at `<DocumentsRoot>/Cubic Odyssey/save/<SteamID32>/`:
   `meta.sav`, `93_blueprints.sav`, `93_servers.sav`, `93_stats.sav` —
   total 186 B on this user's machine. The original PLAN.md treated
   slots as the only atomic unit; reality has this account-level layer
   above slots. `SaveAccount` models it; Phase 4 must snapshot these
   independently of slots.
2. **Single inner account folder `0/`** — no `1/`. Plan must enumerate
   dynamically (already does).
3. **TGA header byte 2 = 0x02** (uncompressed RGB) — Phase 5's TGA
   decoder can start with the uncompressed-only path; RLE only if a
   different save surfaces it.
4. **Steam Cloud `remote/` doesn't exist on this machine yet**, only
   `remotecache.vdf`. `SaveLocator` records the cloud source as
   `Exists=false` so the watcher (Phase 6) can pick it up the moment
   the directory appears.
5. **Multiple Steam libraries on this machine symlink compatdata to
   one underlying Proton prefix.** Without canonical-path dedup, the
   UI would show 4× duplicate slot cards. `SaveLocator.DedupByCanonicalPath`
   handles it; `SteamLocator.Canonicalize` resolves intermediate symlinks
   by walking path segments (the .NET stdlib `ResolveLinkTarget` only
   resolves leaf links, which is insufficient here).

## What's next: Phase 3 (Onboarding + settings persistence)

Per `docs/PLAN.md`:

1. **First-run onboarding wizard**: welcome screen, detected sources
   summary, pick backup root (default
   `LocalApplicationData/CubicOdysseyVault/snapshots`), enable file
   watcher (default Yes), retention defaults.
2. **`AppSettingsService`** — JSON persistence under
   `Environment.SpecialFolder.ApplicationData / "CubicOdysseyVault" /
   "settings.json"`. Mirror
   `/run/media/james/SSD/My_Programs_SSD/OpenFATX/OpenFATX.UI/Services/WindowSettingsService.cs`
   pattern (silent failure, try/catch).
3. **Settings dialog** — accessible from a toolbar button; lets the user
   change backup root, manual-source overrides, retention numbers,
   watcher on/off.
4. Wire `SaveLocator.LocateSources(roots, manualSourceRoots)` to read
   manual overrides from settings on each refresh.
5. (Polish, optional in Phase 3) Resolve Steam display names from
   `<root>/userdata/<SteamID32>/config/localconfig.vdf`. Sidebar
   currently shows raw `SteamID32`.

## What was explicitly decided this session

| Question | Choice |
|---|---|
| Scan trigger | **Auto-scan on launch** via DataContextChanged first-time fire; manual Refresh button stays for re-scans |
| Account-level UI | **Single card above the slot grid** showing file count + size + source |
| Account-level data model | New `SaveAccount` record, parallel to `SaveSlot`, scoped to SteamID32 only (the inner `0`/`1` folders are part of `SaveSlot` identity) |

## Open assumptions to validate when data lands

- **Steam Cloud `remote/` layout**: inferred from `remotecache.vdf` paths
  to be `<userdata>/<id>/<app>/remote/Cubic Odyssey/save/<SteamID32>/...`
  (mirrors compatdata one level deeper). Validate the first time Cloud
  sync materializes files. If the layout differs, adjust
  `SaveLocator.AddSteamCloudRemoteSources`.
- **Two-account-folder semantics (`0` vs `1`)**: still unknown. Only `0/`
  exists on this account. The plan correctly enumerates dynamically.
- **TGA RLE variant**: not yet observed. Phase 5's decoder starts with
  uncompressed-only; if a future save uses RLE (image_type byte = 0x0A),
  the decoder needs an RLE branch.
- **Slot file naming variants**: `93_<8-hex>.{sav,vw3}`, `93_<name>.sav`,
  `ship_<n>.vx`, `screenshot.tga` confirmed. Don't filter by name — the
  enumerator + future snapshot pipeline must capture every file in the
  slot folder verbatim.

## Style template — non-negotiable (unchanged)

All Avalonia code in this project must mirror the patterns in
`/run/media/james/SSD/My_Programs_SSD/OpenFATX/`:

- .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm 8.4.0, xUnit 2.4.2
- `[ObservableProperty]` for VM state, `[RelayCommand]` for commands
- ViewModel exposes `Func<...>?` callbacks for dialogs; View wires them
  in `DataContextChanged` (see `OpenFATX.UI/Views/MainWindow.axaml.cs`)
- Dark theme + red accent palette (already copied)
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
../CubicOdysseyVault.UI/App.axaml                   FluentTheme + DarkTheme include
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml      palette
../CubicOdysseyVault.UI/ViewModels/                 MainWindow / SteamUser / SaveAccount / SaveSlot / SaveSource VMs
../CubicOdysseyVault.UI/Views/MainWindow.axaml      sidebar + account card + slot WrapPanel
../CubicOdysseyVault.UI/Views/MainWindow.axaml.cs   auto-fire discovery on first attach
../CubicOdysseyVault.Desktop/Program.cs             entry point
../CubicOdysseyVault.Tests/                         25 tests, all passing
```
