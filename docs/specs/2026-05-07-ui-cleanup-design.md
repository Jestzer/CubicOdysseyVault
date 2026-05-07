# UI Cleanup Design — 2026-05-07

A polish pass on the Avalonia desktop UI before any new feature work (next being Phase 2: map viewer). This is design discipline only — no new functionality, no layout restructuring.

## Context

The app is functionally complete (Phase 10 + Phase 1 of new-work shipped, 141 tests passing) but **aesthetically unpolished**. The root cause is a single structural issue: `Themes/DarkTheme.axaml` defines only a color palette; every other styling decision (font sizes, spacing, padding, corner radii, control templates) is **inline** on individual XAML elements. Result: ~20 distinct font sizes scattered 9–22px, ~11 distinct spacing values from 2–24px, default Fluent buttons everywhere, no reusable card/pill/list-row patterns. Polishing without fixing this is whack-a-mole.

The user's pain points (from brainstorming session): visual hierarchy doesn't pop, spacing/alignment feels random, components look default-Fluent, layout feels cramped/random (not structurally wrong — just unpolished).

## Decisions anchoring the design

- **Layout**: keep the current 3-pane shape (toolbar / Steam-users sidebar / slot grid + detail pane). No structural change.
- **Mood**: game-aware atmospheric. Subtle gradient surfaces, more accent presence in chrome and selected states, soft elevation on cards, voxel-themed micro-detail (corner notch on the *focused* slot card only). Stops short of overbearing — no starfields, no neon, no atmospheric chrome.
- **Theme**: dark only. Tokens are named semantically (`Surface0`, `BorderSubtle`, etc.) so a future light variant is a smaller change, but light mode is **out of scope here**.
- **Palette**: keep the existing red+dark mood. No hue shift.

## 1. Design tokens (`Themes/Tokens.axaml` — new)

### Colors (semantic naming)

Existing brushes are kept for backward compatibility (`AccentBrush`, etc.) and aliased to the new semantic names where they map.

| Token | Value | Use |
|---|---|---|
| `Surface0Brush` | `#2D2D30` (existing `MainBackground`) | Page canvas |
| `Surface1Brush` | `#252526` (existing `SidebarBackground`) | Sidebar |
| `CardSurfaceBrush` | LinearGradient `#2C2D33` → `#1F1F24` (180°) | Slot cards, account-level card, detail panes |
| `Surface3Brush` | `#3F3F46` (existing `SidebarBlock`) | Raised / highlighted blocks |
| `BorderSubtleBrush` | `#FFFFFF` @ 5% alpha | Hairlines, dividers |
| `BorderDefaultBrush` | `#FFFFFF` @ 10% alpha | Card / button outlines |
| `BorderStrongBrush` | `#FFFFFF` @ 18% alpha | Inputs, focus indication |
| `AccentBaseBrush` | `#C62828` (existing `AccentColor`) | Primary buttons, key chrome |
| `AccentBrightBrush` | `#E53935` (existing `AccentColorSecondary`) | Hover / active |
| `AccentSurfaceBrush` | `#C62828` @ 18% alpha | Selected row tint |
| `AccentBorderBrush` | `#C62828` @ 45% alpha | Selected card border, focus ring |
| `TextPrimaryBrush` | `#FFFFFF` (existing) | Body, titles |
| `TextSecondaryBrush` | `#C5C5C5` (**brightened from existing `#989898`**) | Sub-labels — current value is too dim |
| `TextMutedBrush` | `#888888` | Eyebrows, captions, timestamps |
| `TextDisabledBrush` | `#5A5A5A` | Inactive states |
| `HealthHealthyBrush` | `#4CAF50` (kept) | Healthy pill |
| `HealthSuspectBrush` | `#FFB300` (kept; renamed from `HealthSuspicious`) | Suspect pill |
| `HealthCorruptedBrush` | `#E53935` (kept) | Corrupted pill |
| `TriggerManualBrush` | reuses `AccentBaseBrush` | MANUAL pill |
| `TriggerAutoBrush` | reuses `HealthSuspectBrush` (amber) | AUTO pill |
| `TriggerPreRestoreBrush` | `#4682C8` (new — desaturated blue) | PRE-RESTORE pill |
| Inventory category brushes (8) | existing values kept | Category pills in SaveInspector |

### Spacing scale

Replace all hardcoded margin/padding values with named tokens. Use by `{StaticResource SpaceMd}` etc.

| Token | px | Use |
|---|---|---|
| `SpaceXs` | 4 | Inline gaps (icon↔text), pill internal padding |
| `SpaceSm` | 8 | Field-to-field, card body row gaps |
| `SpaceMd` | 12 | Card padding, list-row padding, button padding-x |
| `SpaceLg` | 16 | Section padding, dialog body padding |
| `SpaceXl` | 24 | Section-to-section, panel inset |
| `Space2Xl` | 32 | Page margins, hero spacing |
| `Space3Xl` | 48 | Empty-state breathing room |

### Typography (Inter, ten roles)

Defined as `TextBlock` styles in `Components.axaml` keyed by `Classes`. Replaces ~20 distinct inline `FontSize` values.

| Style class | Spec | Use |
|---|---|---|
| `display` | 22 / 600 | Onboarding welcome heading |
| `heading` | 18 / 600 | Section / dialog titles |
| `title` | 15 / 600 | Slot names, panel titles |
| `subtitle` | 14 / 500 | Section subheads |
| `body` | 13 / 400 (default) | Reading text |
| `body-emph` | 13 / 600 | Emphasized inline values |
| `body-sm` | 12 / 400 | Secondary labels, hints |
| `label` | 11 / 600 / `letter-spacing 0.14em` / uppercase | Eyebrow labels |
| `caption` | 10 / 400 | Captions, fine print |
| `numeric` | 13 / 400 / tabular nums / monospace | Sizes, counts, timestamps |

### Corner radii

| Token | px | Use |
|---|---|---|
| `RadiusSm` | 3 | Pills, chips |
| `RadiusMd` | 5 | Buttons, list rows |
| `RadiusLg` | 8 | Cards, panels |
| `RadiusXl` | 12 | Dialogs, hero frame |

### Elevation (BoxShadow strings)

Used sparingly. Most surfaces stay flat.

| Token | Value | Use |
|---|---|---|
| `ElevFlat` | `none` | Sidebar items, list rows |
| `ElevSubtle` | `0 1 3 #66000000` | Cards (default) |
| `ElevRaised` | `0 4 14 #80000000` | Cards on hover, dropdowns |
| `ElevFloat` | `0 12 32 #99000000` | Modal dialogs |

## 2. Component styles (`Themes/Components.axaml` — new)

Avalonia `Style` selectors, applied via `Classes="..."` on consumers. All consume the tokens above — no hardcoded values.

### Buttons (5 variants)

`Button.primary`, `Button.secondary`, `Button.ghost`, `Button.destructive`, `Button.icon`. All share base padding and radius (`SpaceSm` × `SpaceMd`, `RadiusMd`); only background, foreground, and border differ. `Button.icon` is a 28×28 square with no text.

| Variant | Use | Background | Border | Hover |
|---|---|---|---|---|
| `primary` | One per dialog (Save, Restore, Back up now) | `AccentBaseBrush` | `AccentBaseBrush` lightened | bg → `AccentBrightBrush` |
| `secondary` | Most actions (Cancel, Browse, Inspect) | `Surface2 @ alpha 0.06` | `BorderDefaultBrush` | bg → +5% alpha |
| `ghost` | Toolbar (Refresh, Settings, Open folder) | transparent | transparent | bg → 5% white |
| `destructive` | Delete confirm + history rows | transparent | `AccentBorderBrush` | bg → `AccentSurfaceBrush` |
| `icon` | Action rows, toolbar without text | transparent | transparent | bg → 6% white |

### Slot card

Single style on `Border.card` with three states via classes:
- Default — `CardSurfaceBrush` background, `BorderDefaultBrush` 1px border, `RadiusLg`, `ElevSubtle`.
- `:pointerover` — slight `RenderTransform="TranslateTransform(0,-1)"`, `ElevRaised`, border lifts to ~14% alpha.
- `Border.card.selected` — border swaps to `AccentBorderBrush` 1px; `BoxShadow` becomes a two-shadow list: an outer accent ring (`0 0 0 1 #C62828` at ~40% alpha) + the `ElevRaised` shadow underneath. **Voxel notch** is a small `Path` (8×8 triangle in `AccentBaseBrush`) absolutely positioned in the top-right corner inside the card; visibility is bound to the `selected` class via a style selector — only renders when selected.

### Pills

`Border.pill` is the base shape (10px / 700 / `RadiusSm` / 3×8 padding / 1px border). Variant classes set color: `.health-ok`, `.health-sus`, `.health-bad`, `.trigger-manual`, `.trigger-auto`, `.trigger-pre`, plus 8 inventory category variants `.cat-equipment` etc. (use existing inventory brushes).

### List row (snapshot history)

`Grid.list-row` with three columns (badges / time-numeric / actions). States:
- Default — transparent, `BorderSubtleBrush` bottom hairline, action buttons hidden.
- `:pointerover` — bg `+2.5% alpha white`, action buttons fade to opacity 1.
- `.selected` — left-edge 2px border in `AccentBaseBrush`, gradient bg from `AccentSurfaceBrush` to transparent (left→right), action buttons visible.

Action buttons within the row use `Button.icon`.

### Section header / eyebrow label

`TextBlock.label` (defined in typography above) is used standalone or alongside a count or action. No separate component — pure typography.

### Inputs

`TextBox` and `NumericUpDown`:
- Default: `Surface0 @ 4% alpha` background, `BorderStrongBrush` 1px border.
- `:pointerover`: border lifts to ~28% alpha.
- `:focus`: border `AccentBorderBrush @ 0.7`, plus `AccentBorderBrush @ 0.18` 2px outer ring.

## 3. MainWindow changes

Layout structure unchanged. Apply tokens + component styles. Specific changes from current behavior:

1. **Detail panel widened from 360 → 400px.** Current 360 is cramped for the screenshot + history list.
2. **Sidebar gains a Watcher + Storage stat block** (below the user list, separated by `BorderSubtleBrush` divider). Two eyebrow-labeled groups:
   - **Watcher** — binds directly to existing `AppSettings.WatcherEnabled` (ON/OFF dot) and `AppSettings.WatcherDebounceSeconds`. No new state.
   - **Storage** — Total snapshot count and total disk used. Both are **derived** values: requires two new computed/read-only properties on `MainWindowViewModel` (`TotalSnapshotCount`, `TotalDiskUsedBytes`), summed from the loaded `Snapshot` records already held in memory after a refresh. Refreshes when the snapshot list refreshes (existing event). No new commands, no new IO, no new persistence.
3. **Snapshot history row actions hide by default**, fade in on hover/select. Reduces visual noise on long lists.
4. **Account-level backup pill becomes a real card** instead of an inline pill at the top of the slot grid. Same `Border.card` style with custom layout (icon, two-line label, last-captured timestamp, primary "Back up" button).
5. **Toolbar buttons become Ghost variant** (transparent background, hover-only). Current toolbar uses default Fluent buttons — they look heavier than they should.

No behavioral / functional changes. All commands, bindings, ViewModel surface remain identical.

## 4. Dialog changes

All six dialogs apply tokens + component styles. **No layout restructuring** in any dialog.

- **SaveInspectorDialog** — Summary tab: section eyebrows on each block (Character / Inventory / Ships); inventory containers wrap in `Border.card`; item rows use `Border.pill.cat-*` for category badges (already partially there). Files tab: nested Decoded/Strings/Hex tabs unchanged structurally; TreeView gets a styled `TreeViewItem` template (eyebrow tag/type labels, monospace value via `Classes="numeric"`). Hero screenshot block uses `Border.hero` (gradient background + `AccentBorderBrush @ 0.10` outer glow + `BorderDefaultBrush` 1px).
- **SettingsDialog** — Form gets section eyebrows (Backups / Sources / Watcher / Retention). All `TextBox` and `NumericUpDown` get the new input styling. Footer becomes `[Button.secondary "Cancel"] [Button.primary "Save"]`.
- **OnboardingDialog** — Step 1 picks up a `TextBlock.display` heading. Step 2 reuses Settings form.
- **RestoreConfirmDialog** — Hero screenshot wrapped in `Border.hero`. Game-running warning becomes a `Border.pill.health-sus` (suspect/amber). Footer: `[Button.secondary "Cancel"] [Button.primary "Restore"]`.
- **TagEditDialog** — `TextBox` + footer. Trivial inheritance.
- **DeleteConfirmDialog** — Footer: `[Button.secondary "Cancel"] [Button.destructive "Delete"]`. Trivial inheritance.

## 5. File organization

```
CubicOdysseyVault.UI/Themes/
├── Tokens.axaml        ← colors, brushes, spacing, radii, elevation values
├── Components.axaml    ← typography styles + control styles (Button, Border for cards/pills, ListBoxItem, TextBox, TreeViewItem)
└── DarkTheme.axaml     ← merges Tokens + Components, exposed at App.axaml level
```

Existing `Themes/DarkTheme.axaml` shrinks to a `<Styles>` element merging `Tokens.axaml` and `Components.axaml`. The existing brush names (`AccentBrush`, `MainBackgroundBrush`, etc.) move into `Tokens.axaml` and **stay defined** so consumers don't break — additive migration. View-by-view cleanup of inline styling can happen incrementally.

`App.axaml` line referencing `DarkTheme.axaml` is unchanged.

## 6. Phasing

Three commits, each independently shippable:

**Phase A — Foundation (no view changes)**
- Create `Tokens.axaml` and `Components.axaml`.
- Shrink `DarkTheme.axaml` to a merger, preserving existing brush names.
- All 141 existing tests pass; build clean.
- Visible diff: zero (foundation is additive).

**Phase B — MainWindow refit**
- Refit `Views/MainWindow.axaml` to use tokens + component styles.
- Implement the 5 specific changes from §3 (detail width, sidebar stat block, history-row action fade, account-level card, ghost toolbar buttons).
- Update `MainWindowViewModel` only if needed for stat-block bindings (sidebar Watcher / Storage values already exist on `AppSettings` and `BackupCoordinator`; minimal binding plumbing).

**Phase C — Dialogs**
- One commit per dialog or grouped, per implementer's preference. Suggested order: SaveInspectorDialog (most complex) → SettingsDialog → OnboardingDialog (shares Settings form) → RestoreConfirmDialog → TagEditDialog → DeleteConfirmDialog.

Each phase can be paused between for review. The user can stop after any phase if priorities shift.

## 7. Out of scope

Explicitly **not** part of this work:

- Light mode / theme toggle.
- Layout structural changes (no master-detail, no list-view variant — three panes stay).
- New features (map viewer, save diffing, anything from PLAN.md Phase 2).
- New ViewModels or new commands. The only ViewModel change is two derived properties on `MainWindowViewModel` for the sidebar Storage stat block (§3 item 2); no new state, no new commands.
- Localization, accessibility audit (separate concern, separate pass).
- New dependencies. Avalonia 11 + existing packages only.
- Per-control template overrides beyond what's listed.

## 8. Verification

**Per-phase:**
1. `dotnet build` clean.
2. `dotnet test` — all 141 existing tests pass (no new tests required; this is pure styling).
3. `dotnet run --project CubicOdysseyVault.Desktop` — app launches, MainWindow renders without exceptions.
4. Visual smoke check (screenshot expected against the design mockups; mockups stored in `.superpowers/brainstorm/86510-1778158894/content/`):
   - Phase A: identical appearance to current.
   - Phase B: MainWindow matches `main-window-polished.html` mockup. Verify: slot card hover lift, slot card selected (red border + voxel notch), history row hover (action buttons fade in), history row selected (red left edge + gradient).
   - Phase C: each dialog opens, primary/secondary buttons visually distinct from current, inputs show focus glow, form sections have eyebrow labels.

**Cross-phase regressions to watch:**
- Existing health badges still color correctly across Healthy/Suspect/Corrupted.
- Inventory category icons + pills still display in SaveInspector (`IconAtlasCache` and existing inventory item bindings unchanged).
- Onboarding still appears on first run (HasCompletedOnboarding flag flow unchanged).
- File watcher status reflects setting changes (sidebar block reads from existing `AppSettings.WatcherEnabled`).

## References

- Brainstorm artifacts (local, not in git): `.superpowers/brainstorm/86510-1778158894/content/`
  - `design-tokens.html` — token visual reference
  - `components.html` — component variants reference
  - `main-window-polished.html` — full MainWindow target mockup
- Existing palette: `CubicOdysseyVault.UI/Themes/DarkTheme.axaml`
- Existing views: `CubicOdysseyVault.UI/Views/*.axaml`
