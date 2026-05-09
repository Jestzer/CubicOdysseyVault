# Cubic Odyssey Vault

A cross-platform Avalonia desktop app that backs up Cubic Odyssey saves and
lets you browse, inspect, tag, restore, and prune them.

Built because Cubic Odyssey's save system is fragile: crashes can zero out
save files, the Quit button sometimes skips the final flush, Steam Cloud has
propagated corrupted saves to other machines, and the game's ~20-slot FIFO
silently buries good manual saves under autosaves. There is no
developer-supplied recovery path. This tool keeps an out-of-band history with
integrity checks and atomic restores.

## How saves in Cubic Odyssey work

This section is what we've reverse-engineered from the user's actual save data
plus the game's `data/configs` and `data/sprites` files. Most of it isn't
documented anywhere; the parsers in `Core/SaveContent/` are the canonical
reference.

### Folder layout

The game writes saves under `Cubic Odyssey/save/<SteamID32>/`. Each Steam user
gets one directory of account-level files plus one subfolder per in-game
account, which in turn holds one subfolder per slot:

```
Cubic Odyssey/save/
└── <SteamID32>/                       ← account-level (cross-slot) data
    ├── meta.sav
    ├── 93_blueprints.sav              ← unlocked recipes
    ├── 93_servers.sav                 ← multiplayer/server list
    ├── 93_stats.sav                   ← global play stats
    └── <account e.g. 0>/              ← game's "account" tier (cosmetic;
        └── <slot e.g. 0>/             ←  most users only ever have one)
            ├── screenshot.tga         ← title-screen preview
            ├── 93_meta.sav            ← slot timestamp + character
            ├── 93_client_state.sav    ← player inventory, equipment, hotbar
            ├── 93_economy.sav         ← currencies / market state
            ├── 93_quests.sav          ← quest progress
            ├── 93_sv_state.sav        ← server-side world state
            ├── 93_<hex>.sav           ← per-region world data, paired with…
            ├── 93_<hex>.vw3           ←  …its voxel chunk file
            └── ship_<N>.vx            ← ship designs (one file per ship)
```

A typical slot is ~30 files / 5–10 MB; account-level data is small (a handful
of files, < 200 KB).

Steam writes these to one of three locations depending on your install:

| Platform | Path |
|---|---|
| Linux/Proton | `<Steam>/steamapps/compatdata/3400000/pfx/drive_c/users/steamuser/Documents/Cubic Odyssey/save/` |
| Windows | `%USERPROFILE%\Documents\Cubic Odyssey\save\` (also OneDrive Documents if redirected) |
| Steam Cloud | `<Steam>/userdata/<SteamID32>/3400000/remote/Cubic Odyssey/save/` |

`3400000` is Cubic Odyssey's Steam appid.

### .sav file format

Every `*.sav` is a thin envelope around a zstd-compressed TLV blob:

```
[u32 LE  decompressed_size]
[zstd frame  (magic 28 B5 2F FD …)]
```

After decompression the payload is a tag-length-value document:

```
header:    u32          (always 0x00000008 in everything we've seen)
count:     u16          number of top-level entries
entries:   count × {
    tag:    u16
    type:   u16
    length: u32
    data:   length bytes
}
```

Recognized types: `1` (u8), `4` (i32), `8` (u32), `9` (i64), `10` (f32),
`11` (f64), `23` (nested TLV — `data` is itself a `count + entries` block).
Anything else passes through as opaque bytes.

`93_meta.sav` is the easiest one to read: tags 5–10 round-trip the slot's
local-time save timestamp as month / day / year / hour / minute / second
(verified against file mtime). The character name lives elsewhere in the same
file — currently fished out by scanning for the longest printable-ASCII run
since we haven't pinned down which tag holds it.

`93_client_state.sav` stores inventory containers as flat sequences of item
records. Each item is a TLV record whose tag-1 string is the item identifier
(`cloth.suit.2`, `wep.mining_laser.4`, `res.battery.3`, …) followed by
durability (float) and count (i32). Container grouping (Equipped / Quickslots
/ Inventory / Ship cargo) is fenced by literal `inventory` and `__quickslots`
ASCII markers. The full TLV schema for inventory containers varies by
container type; the inspector uses the byte-pattern shortcut in
`InventoryExtractor.cs` rather than fully decoding every variant.

### Item catalog & icons

The game ships per-item metadata as plain-text `.cfg` files at
`<install>/data/configs/items/*.cfg` (~1300 entries). Each one is a single
`ItemCfg { identifier "…" type GEAR tier 2 inv_frame 67 … }` block. The
inspector loads these to humanize identifiers (`cloth.suit.2` →
`Suit (Tier 2)`) and to pull `inv_frame` for the icon lookup.

Inventory icons live in `<install>/data/sprites/items01.png` — a 2720×3500
RGBA atlas. Rectangles are described in `items01.bspr`:

```
"BSPR" magic + u32 header
records:  N × 12 bytes  [u32 reserved=0][u16 x][u16 y][u16 w][u16 h]
trailer:  M × 4 bytes   [u16 frame_id][u16 count]   ← animation grouping
"items01.png\0" + padding
```

Each item's `inv_frame` indexes directly into the record array. The icons
form a regular 160×160 grid covering ~600 sprites. (Note: the much smaller
`icons.bspr` / `icons.png` is the UI/HUD atlas — slot frames, cursor glyphs,
etc. — not inventory icons. Easy to mis-target.)

### Why this tool exists

The format itself is reasonable; the *operational* parts are not. From a year
of playing and from incident reports the author has collected:

- The game keeps a small (~20-slot) FIFO of automatic saves. New autosaves
  silently bury older manual saves once the ring fills up.
- Crashes mid-write leave files as runs of NULL bytes — the integrity
  checker flags any `.sav` that's all zeros and refuses to promote it into
  a snapshot.
- The Quit button has been observed to return before the final flush
  completes, leaving a half-written slot on disk.
- Steam Cloud will happily propagate a corrupted save to every other
  machine on the account, overwriting good local copies.
- There is no developer-supplied recovery path. Once a slot is gone it's
  gone.

This tool keeps an out-of-band history with content-addressed dedup, integrity
checks, and atomic restores so none of the above turns into permanent data
loss.

## Features

- **Discovery** — finds Steam roots cross-platform (candidate paths,
  `libraryfolders.vdf` parsing, `HKCU\Software\Valve\Steam\SteamPath` on
  Windows), then walks each `Cubic Odyssey/save/<SteamID32>/<account>/<slot>/`
  layout under Proton compatdata, Windows Documents (incl. OneDrive
  redirection), and Steam Cloud `userdata/.../remote/`.
- **Manual snapshots** — per-slot "Back up now" button. Snapshots are
  copied atomically (each file written as `*.tmp` then renamed) into
  `<backupRoot>/snapshots/<SteamID32>/<account>/<slot>/<UTC-stamp>__<6-hex>/`.
  An identical re-snapshot is detected via a deterministic combined SHA-256
  and skipped — no churn from re-running the same backup.
- **Auto snapshots** — opt-in `FileSystemWatcher` per source with a
  configurable per-key debounce window. Auto refuses to snapshot a slot
  whose integrity check flags it `Corrupted` so a half-written save never
  gets promoted into the store.
- **Tiered retention** — keep N hourly + M daily + K weekly auto snapshots
  (defaults 24/14/8) with UTC-anchored bucket boundaries. Manual,
  Pre-restore, and tagged snapshots are kept forever.
- **Tag / rename / delete** snapshots from the history list. Tagging an
  Auto snapshot promotes it into the "always keep" tier without re-running
  the snapshot.
- **Restore** with a Pre-restore safety net — the live state is captured
  before the swap so the restore is itself undoable. Refuses to run while
  Cubic Odyssey is open (uses `pgrep -f` on Linux/macOS to match the
  wine-hosted .exe; literal process-name match on Windows).
- **Slot card thumbnails** — `screenshot.tga` decoded in-process (no native
  dep) and displayed alongside health and trigger badges.
- **Onboarding wizard** on first run + always-available Settings dialog.
  Settings persist as JSON under `%APPDATA%\CubicOdysseyVault\settings.json`.

## Build & run

Requires the .NET 8 SDK.

```sh
dotnet build CubicOdysseyVault.sln
dotnet test CubicOdysseyVault.sln
dotnet run --project CubicOdysseyVault.Desktop
```

The same commands work on Linux, macOS, and Windows. The `Desktop` project
is the entry point; `Core` holds engine logic (no Avalonia dependency) and
`UI` holds Avalonia views, view-models, and services.

## Project layout

```
CubicOdysseyVault.Core/      pure C# domain logic, no Avalonia refs
  Steam/                     SteamLocator + libraryfolders.vdf parser
  Saves/                     SaveLocator + per-slot enumerator
  Integrity/                 SHA-256 + NULL-block detection + TGA header check
  Snapshots/                 BackupService + manifest + RetentionPolicy
  Tga/                       uncompressed-RGB TGA decoder
  Watching/                  FileSystemWatcher + per-key debounce
  Restore/                   atomic swap + game-running guard
CubicOdysseyVault.UI/        Avalonia 11 + CommunityToolkit.Mvvm 8
  Services/                  AppSettingsService, BackupCoordinator, TgaBitmapLoader
  ViewModels/
  Views/                     MainWindow + Settings/Onboarding/Restore/Tag/Delete dialogs
  Themes/                    DarkTheme.axaml palette
CubicOdysseyVault.Desktop/   entry point (Program.cs, app.manifest)
CubicOdysseyVault.Tests/     89 xUnit tests covering Core
```

## On-disk shape

The backup root (default `%LOCALAPPDATA%\CubicOdysseyVault\`,
configurable in Settings) holds:

```
<backupRoot>/snapshots/
└── <SteamID32>/
    ├── _account/                          ← shared cross-slot data
    │   ├── manifest.json
    │   └── 2026-05-06T14-30-12Z__abc123/
    │       ├── meta.sav
    │       ├── 93_blueprints.sav
    │       └── ...
    └── <accountFolder e.g. 0>/
        └── <slotName e.g. 3>/
            ├── manifest.json              ← per-slot history
            └── 2026-05-06T14-30-12Z__abc123/
                ├── screenshot.tga
                ├── 93_meta.sav
                └── ... (every file in the slot, verbatim)
```

Each `manifest.json` is a JSON document of `Snapshot` records:

```json
{
  "SchemaVersion": 1,
  "Snapshots": [
    {
      "Id": "2026-05-06T14-30-12Z__abc123",
      "CapturedAtUtc": "2026-05-06T14:30:12Z",
      "Trigger": "Manual",
      "Tag": "before-boss",
      "CombinedHash": "sha256:...",
      "FileHashes": { "meta.sav": "...", "screenshot.tga": "..." },
      "TotalBytes": 4928036,
      "Health": "Healthy",
      "SourceKind": "ProtonCompatdata",
      "FolderName": "2026-05-06T14-30-12Z__abc123"
    }
  ]
}
```

Restoring keeps the previous live state in `<slot>.replaced-<UTC>/`
alongside the live folder for one generation, so the restore itself can
be rolled back manually if needed.

## Save sources

Per-platform save locations are documented above in
[How saves in Cubic Odyssey work](#how-saves-in-cubic-odyssey-work). Manual
override paths can be added in Settings — useful for external drives or
recovered backups from another machine.

## Status

All ten phases of the original implementation plan are done. 89 unit
tests cover the engine; UI exercises were verified by launching the app
and watching it run discovery against this developer's actual save data.
The Windows-side path resolution and registry probe compile but haven't
been runtime-validated on a Windows host yet — runtime smoke-test on
Windows is the one remaining loose end.

- `docs/PLAN.md` — full design spec (read this for the why)
- `docs/HANDOFF.md` — what shipped + what's still loose

## Commit messages

Every commit subject must start with one of:

| Prefix | Use for |
|---|---|
| `feat:` | a new user-visible feature |
| `fix:` | a bug fix |
| `refactor:` | a change that neither adds a feature nor fixes a bug |
| `test:` | adding or changing tests |
| `doc:` | documentation only (README, comments, design docs) |
| `style:` | formatting, whitespace, code-style tweaks with no behavior change |
| `chore:` | tooling, build, dependencies, repo housekeeping |

Keep the subject short and imperative — e.g. `feat: discover save folders on Linux/Proton`.
