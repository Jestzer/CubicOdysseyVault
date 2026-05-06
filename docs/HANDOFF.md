# Handoff — pick up from Phase 1

> Written 2026-05-06 at the end of the skeleton phase, just before the user
> switched to a different Linux account that has actual Cubic Odyssey saves.
> Read this first when resuming work.

## Where we are

**Phase 1 (Skeleton) is done.** The solution at this directory's root builds
clean (`dotnet build CubicOdysseyVault.sln` → 0 warnings, 0 errors), the one
sanity test passes, and `dotnet run --project CubicOdysseyVault.Desktop`
opens a 1200×700 dark window titled "Cubic Odyssey Vault" with sidebar /
content / status-bar placeholders. Theme palette is copied from
`OpenFATX/OpenFATX.UI/Themes/DarkTheme.axaml` (red accent retained for now).

**Next: Phase 2 (Discovery).** Implement these in `CubicOdysseyVault.Core`:

- `Steam/SteamLocator.cs` — discover Steam install roots cross-platform
- `Steam/SteamRoot.cs` — record type
- `Steam/LibraryFoldersVdfParser.cs` — parse `libraryfolders.vdf` to find extra Steam libraries
- `Saves/SaveLocator.cs` — given Steam roots + AppID 3400000, return candidate save sources
- `Saves/SaveSource.cs` — discriminated kind: `Documents | ProtonCompatdata | SteamCloudRemote | Manual`
- `Saves/SaveSlot.cs` — record per slot
- `Saves/SaveSlotEnumerator.cs` — walks `<source>/<SteamID32>/<account>/<slot>/`

Wire results into `MainWindowViewModel` (sidebar shows discovered Steam users,
content shows their slots) so we can verify discovery against real saves.

## What to verify on the new account first

The whole point of switching accounts is to ground discovery in real data
instead of guesses. Before writing Phase 2 code, **inspect a real slot**:

```bash
ls -la ~/.steam/steam/steamapps/compatdata/3400000/pfx/drive_c/users/steamuser/Documents/Cubic\ Odyssey/save/ 2>/dev/null
ls -la ~/.local/share/Steam/userdata/*/3400000/remote/ 2>/dev/null
ls -la "/run/media/james-work/SSD/Program Files (x86)/Steam/steamapps/compatdata/3400000/" 2>/dev/null
```

Confirm:
1. Exact directory hierarchy (`<SteamID32>/<account>/<slot>/`).
2. Filenames and extensions inside one slot folder (research surfaced
   `screenshot.tga`, `meta.sav`, `93_meta.sav`, `voxworld3_*.sav`,
   `vw3_*_0.sav` but the exhaustive list is unknown).
3. Whether the `0` and `1` account-level folders both exist.
4. Whether `screenshot.tga` is uncompressed or RLE-compressed TrueColor
   (`xxd <file> | head` — first 18 bytes are the TGA header; image type at
   byte 2 is 2 for uncompressed RGB or 10 for RLE RGB).
5. Whether Steam Cloud `userdata/.../remote/` mirrors the local files or has
   its own layout.

Save a screenshot or notes of what's there — useful when implementing the
TGA decoder in Phase 5.

## What was explicitly decided

| Question | Choice |
|---|---|
| Detail-view depth | **Hybrid** — metadata + screenshot preview now; binary-parsing hooks for v2 |
| Backup trigger | **Both** manual button + opt-in `FileSystemWatcher` with debounce + integrity verification |
| Retention | **Tiered/generational** — last 24 hourly + last 14 daily + last 8 weekly + tagged forever |

Project name placeholder: **CubicOdysseyVault** (rename freely if user wants
something different — touches `*.csproj` filenames, `*.sln`, namespaces, and
`AssemblyName` in `Desktop.csproj`).

## Style template — non-negotiable

All Avalonia code in this project must mirror the patterns in
`/run/media/james-work/SSD/My_Programs_SSD/OpenFATX/`:

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
PLAN.md                                        full design spec (this dir)
HANDOFF.md                                     this file
../Directory.Build.props                       net8.0 + Avalonia 11.2.3
../CubicOdysseyVault.Core/Constants.cs         CubicOdysseyAppId = 3400000
../CubicOdysseyVault.UI/App.axaml              FluentTheme + DarkTheme include
../CubicOdysseyVault.UI/Themes/DarkTheme.axaml palette
../CubicOdysseyVault.UI/Views/MainWindow.axaml current placeholder window
../CubicOdysseyVault.Desktop/Program.cs        entry point
../CubicOdysseyVault.Tests/ConstantsTests.cs   sanity test
```

## Open risks worth re-reading in PLAN.md

- Save file naming variants — snapshot whole slot folder verbatim
- Worlds vs characters — assumed bundled per slot; revisit if RE shows otherwise
- TGA variant — uncompressed-only decoder first, RLE if needed
- Steam Cloud authority can overwrite restored saves on next launch
- `0`/`1` account folder semantics — unconfirmed
