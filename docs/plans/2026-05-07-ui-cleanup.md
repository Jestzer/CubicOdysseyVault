# UI Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace inline ad-hoc styling across the Avalonia UI with a real design system (tokens + reusable styles), then refit MainWindow and the six dialogs to use it.

**Architecture:** Two new theme files — `Themes/Tokens.axaml` (resources: colors, brushes, spacing, radii) and `Themes/Components.axaml` (style selectors: typography, buttons, cards, pills, list rows, inputs). Existing `Themes/DarkTheme.axaml` keeps all existing brush keys for backward compatibility and gains a merge of `Tokens.axaml`. Each view migrates to use `Classes="..."` selectors and resource references (`{StaticResource Pad*}` for uniform Padding/Margin, `{StaticResource Space*}` for StackPanel.Spacing, `{StaticResource Radius*}` for CornerRadius) instead of hardcoded property values. No layout restructuring; no behavior changes except the few flagged in §3 of the spec.

**Tech Stack:** Avalonia 11 (.NET 8), Inter font (already wired), CommunityToolkit.Mvvm 8.4, xUnit. No new dependencies.

**Spec reference:** `docs/specs/2026-05-07-ui-cleanup-design.md` — read before starting.

---

## Phase A — Foundation (2 tasks)

### Task 1: Create `Tokens.axaml`

Adds new design tokens additively. Existing brushes in `DarkTheme.axaml` are untouched in this task — Tokens lives alongside, reachable via `MergedDictionaries`. After this task: tokens available globally, no visible change anywhere.

**Files:**
- Create: `CubicOdysseyVault.UI/Themes/Tokens.axaml`
- Modify: `CubicOdysseyVault.UI/Themes/DarkTheme.axaml` (add merge of Tokens.axaml)

- [ ] **Step 1: Create `Themes/Tokens.axaml`**

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Surface (cards) — gradient -->
  <LinearGradientBrush x:Key="CardSurfaceBrush" StartPoint="0%,0%" EndPoint="0%,100%">
    <GradientStop Color="#FF2C2D33" Offset="0"/>
    <GradientStop Color="#FF1F1F24" Offset="1"/>
  </LinearGradientBrush>

  <!-- Borders (white-on-dark tints) -->
  <SolidColorBrush x:Key="BorderSubtleBrush"  Color="#0DFFFFFF"/> <!-- 5%  -->
  <SolidColorBrush x:Key="BorderDefaultBrush" Color="#1AFFFFFF"/> <!-- 10% -->
  <SolidColorBrush x:Key="BorderStrongBrush"  Color="#2EFFFFFF"/> <!-- 18% -->

  <!-- Accent variants -->
  <SolidColorBrush x:Key="AccentSurfaceBrush" Color="#2EC62828"/> <!-- 18% accent -->
  <SolidColorBrush x:Key="AccentBorderBrush"  Color="#73C62828"/> <!-- 45% accent -->

  <!-- Text -->
  <SolidColorBrush x:Key="TextDisabledBrush" Color="#FF5A5A5A"/>

  <!-- Trigger pills (Manual reuses AccentBrush; Auto reuses HealthSuspiciousBrush) -->
  <SolidColorBrush x:Key="TriggerPreRestoreBrush" Color="#FF4682C8"/>

  <!-- Spacing scale — Double resources for StackPanel.Spacing, explicit Width/Height -->
  <x:Double x:Key="SpaceXs">4</x:Double>
  <x:Double x:Key="SpaceSm">8</x:Double>
  <x:Double x:Key="SpaceMd">12</x:Double>
  <x:Double x:Key="SpaceLg">16</x:Double>
  <x:Double x:Key="SpaceXl">24</x:Double>
  <x:Double x:Key="Space2Xl">32</x:Double>
  <x:Double x:Key="Space3Xl">48</x:Double>

  <!-- Same scale as Thickness for Padding/Margin (Avalonia StaticResource doesn't auto-convert Double to Thickness) -->
  <Thickness x:Key="PadXs">4</Thickness>
  <Thickness x:Key="PadSm">8</Thickness>
  <Thickness x:Key="PadMd">12</Thickness>
  <Thickness x:Key="PadLg">16</Thickness>
  <Thickness x:Key="PadXl">24</Thickness>
  <Thickness x:Key="Pad2Xl">32</Thickness>
  <Thickness x:Key="Pad3Xl">48</Thickness>

  <!-- Corner radii -->
  <CornerRadius x:Key="RadiusSm">3</CornerRadius>
  <CornerRadius x:Key="RadiusMd">5</CornerRadius>
  <CornerRadius x:Key="RadiusLg">8</CornerRadius>
  <CornerRadius x:Key="RadiusXl">12</CornerRadius>

  <!-- Elevation reference (use these strings inline as BoxShadow values):
       ElevFlat   = (no shadow)
       ElevSubtle = "0 1 3 #66000000"
       ElevRaised = "0 4 14 #80000000"
       ElevFloat  = "0 12 32 #99000000"
  -->

</ResourceDictionary>
```

- [ ] **Step 2: Merge Tokens into `DarkTheme.axaml`**

Open `CubicOdysseyVault.UI/Themes/DarkTheme.axaml`. Wrap the existing `<ResourceDictionary>` content so that the first child is a `<ResourceDictionary.MergedDictionaries>` element including Tokens.axaml. The simplest pattern:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <ResourceDictionary.MergedDictionaries>
    <ResourceInclude Source="/Themes/Tokens.axaml"/>
  </ResourceDictionary.MergedDictionaries>

  <!-- ===== existing palette unchanged below this line ===== -->
  <Color x:Key="MainBackground">#FF2D2D30</Color>
  <!-- ...etc... -->
</ResourceDictionary>
```

Leave every existing `<Color>` and `<SolidColorBrush>` definition exactly as-is — this task is purely additive.

- [ ] **Step 3: Build to confirm both files parse**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded. No warnings about Tokens.axaml or DarkTheme.axaml.

- [ ] **Step 4: Run tests to confirm no regressions**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 141 tests pass.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Themes/Tokens.axaml \
        CubicOdysseyVault.UI/Themes/DarkTheme.axaml
git commit -m "feat(ui): introduce Tokens.axaml design tokens"
```

---

### Task 2: Create `Components.axaml` and register in `App.axaml`

Adds reusable style selectors. After this task: classes `display`/`heading`/`title`/etc. on TextBlocks, `primary`/`secondary`/`ghost`/`destructive`/`icon` on Buttons, `card`/`pill.*` on Borders all work — but no view uses them yet, so the app still looks identical.

**Files:**
- Create: `CubicOdysseyVault.UI/Themes/Components.axaml`
- Modify: `CubicOdysseyVault.UI/App.axaml`

- [ ] **Step 1: Create `Themes/Components.axaml`**

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- ============================================================
       TYPOGRAPHY — apply via Classes="display|heading|title|..."
       ============================================================ -->
  <Style Selector="TextBlock.display">
    <Setter Property="FontSize" Value="22"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.heading">
    <Setter Property="FontSize" Value="18"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.title">
    <Setter Property="FontSize" Value="15"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.subtitle">
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.body">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.body-emph">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.body-sm">
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.label">
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="LetterSpacing" Value="1.4"/>
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.caption">
    <Setter Property="FontSize" Value="10"/>
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
  </Style>
  <Style Selector="TextBlock.numeric">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontFamily" Value="Cascadia Mono, Consolas, monospace"/>
  </Style>

  <!-- ============================================================
       BUTTONS — apply via Classes="primary|secondary|ghost|destructive|icon"
       ============================================================ -->
  <!-- Primary -->
  <Style Selector="Button.primary">
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Padding" Value="14,7"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.primary /template/ ContentPresenter">
    <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.primary:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="{StaticResource AccentBrushSecondary}"/>
  </Style>
  <Style Selector="Button.primary:pressed /template/ ContentPresenter">
    <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
  </Style>

  <!-- Secondary -->
  <Style Selector="Button.secondary">
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Padding" Value="14,7"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.secondary /template/ ContentPresenter">
    <Setter Property="Background" Value="#0FFFFFFF"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.secondary:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#1AFFFFFF"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderStrongBrush}"/>
  </Style>

  <!-- Ghost -->
  <Style Selector="Button.ghost">
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Padding" Value="12,6"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.ghost /template/ ContentPresenter">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.ghost:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#0DFFFFFF"/>
  </Style>

  <!-- Destructive (outlined red) -->
  <Style Selector="Button.destructive">
    <Setter Property="Foreground" Value="#FFFF8A87"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Padding" Value="14,7"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.destructive /template/ ContentPresenter">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.destructive:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="{StaticResource AccentSurfaceBrush}"/>
  </Style>

  <!-- Icon (square 28x28) -->
  <Style Selector="Button.icon">
    <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    <Setter Property="Width" Value="28"/>
    <Setter Property="Height" Value="28"/>
    <Setter Property="Padding" Value="0"/>
    <Setter Property="HorizontalContentAlignment" Value="Center"/>
    <Setter Property="VerticalContentAlignment" Value="Center"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.icon /template/ ContentPresenter">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
  </Style>
  <Style Selector="Button.icon:pointerover">
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
  </Style>
  <Style Selector="Button.icon:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#1AFFFFFF"/>
  </Style>

  <!-- ============================================================
       CARDS — apply via Classes="card" (and toggle "selected")
       ============================================================ -->
  <Style Selector="Border.card">
    <Setter Property="Background" Value="{StaticResource CardSurfaceBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderDefaultBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{StaticResource RadiusLg}"/>
    <Setter Property="BoxShadow" Value="0 1 3 #66000000"/>
  </Style>
  <Style Selector="Border.card.selected">
    <Setter Property="BorderBrush" Value="{StaticResource AccentBorderBrush}"/>
    <Setter Property="BoxShadow" Value="0 0 0 1 #73C62828, 0 4 14 0 #80000000"/>
  </Style>

  <!-- ============================================================
       PILLS — apply via Classes="pill health-ok|health-sus|..."
       Note: TextBlock inside the pill picks up sizing from > selector.
       ============================================================ -->
  <Style Selector="Border.pill">
    <Setter Property="CornerRadius" Value="{StaticResource RadiusSm}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="7,3"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
  </Style>
  <Style Selector="Border.pill > TextBlock">
    <Setter Property="FontSize" Value="10"/>
    <Setter Property="FontWeight" Value="Bold"/>
    <Setter Property="LetterSpacing" Value="1"/>
  </Style>

  <!-- Health -->
  <Style Selector="Border.pill.health-ok">
    <Setter Property="Background" Value="#284CAF50"/>
    <Setter Property="BorderBrush" Value="#524CAF50"/>
  </Style>
  <Style Selector="Border.pill.health-ok > TextBlock">
    <Setter Property="Foreground" Value="#FF7FD380"/>
  </Style>
  <Style Selector="Border.pill.health-sus">
    <Setter Property="Background" Value="#26FFB300"/>
    <Setter Property="BorderBrush" Value="#52FFB300"/>
  </Style>
  <Style Selector="Border.pill.health-sus > TextBlock">
    <Setter Property="Foreground" Value="#FFFFC757"/>
  </Style>
  <Style Selector="Border.pill.health-bad">
    <Setter Property="Background" Value="#2EE53935"/>
    <Setter Property="BorderBrush" Value="#61E53935"/>
  </Style>
  <Style Selector="Border.pill.health-bad > TextBlock">
    <Setter Property="Foreground" Value="#FFFF8A87"/>
  </Style>

  <!-- Trigger -->
  <Style Selector="Border.pill.trigger-manual">
    <Setter Property="Background" Value="{StaticResource AccentSurfaceBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource AccentBorderBrush}"/>
  </Style>
  <Style Selector="Border.pill.trigger-manual > TextBlock">
    <Setter Property="Foreground" Value="#FFFF8A87"/>
  </Style>
  <Style Selector="Border.pill.trigger-auto">
    <Setter Property="Background" Value="#26FFB300"/>
    <Setter Property="BorderBrush" Value="#52FFB300"/>
  </Style>
  <Style Selector="Border.pill.trigger-auto > TextBlock">
    <Setter Property="Foreground" Value="#FFFFC757"/>
  </Style>
  <Style Selector="Border.pill.trigger-pre">
    <Setter Property="Background" Value="#2E4682C8"/>
    <Setter Property="BorderBrush" Value="#614682C8"/>
  </Style>
  <Style Selector="Border.pill.trigger-pre > TextBlock">
    <Setter Property="Foreground" Value="#FF7FB5E6"/>
  </Style>

  <!-- Inventory categories — use existing brushes for fills, lighter text -->
  <Style Selector="Border.pill.cat-equipment">
    <Setter Property="Background" Value="#284CAF50"/>
    <Setter Property="BorderBrush" Value="#524CAF50"/>
  </Style>
  <Style Selector="Border.pill.cat-equipment > TextBlock">
    <Setter Property="Foreground" Value="#FF7FD380"/>
  </Style>
  <Style Selector="Border.pill.cat-weapon">
    <Setter Property="Background" Value="#2EE53935"/>
    <Setter Property="BorderBrush" Value="#61E53935"/>
  </Style>
  <Style Selector="Border.pill.cat-weapon > TextBlock">
    <Setter Property="Foreground" Value="#FFFF8A87"/>
  </Style>
  <Style Selector="Border.pill.cat-resource">
    <Setter Property="Background" Value="#2E42A5F5"/>
    <Setter Property="BorderBrush" Value="#6142A5F5"/>
  </Style>
  <Style Selector="Border.pill.cat-resource > TextBlock">
    <Setter Property="Foreground" Value="#FF7FB5E6"/>
  </Style>
  <Style Selector="Border.pill.cat-shipcomponent">
    <Setter Property="Background" Value="#2EFFA726"/>
    <Setter Property="BorderBrush" Value="#61FFA726"/>
  </Style>
  <Style Selector="Border.pill.cat-shipcomponent > TextBlock">
    <Setter Property="Foreground" Value="#FFFFAE6C"/>
  </Style>
  <Style Selector="Border.pill.cat-deployable">
    <Setter Property="Background" Value="#2EAB47BC"/>
    <Setter Property="BorderBrush" Value="#61AB47BC"/>
  </Style>
  <Style Selector="Border.pill.cat-deployable > TextBlock">
    <Setter Property="Foreground" Value="#FFD09BE5"/>
  </Style>
  <Style Selector="Border.pill.cat-key">
    <Setter Property="Background" Value="#33FFD54F"/>
    <Setter Property="BorderBrush" Value="#66FFD54F"/>
  </Style>
  <Style Selector="Border.pill.cat-key > TextBlock">
    <Setter Property="Foreground" Value="#FFE7D575"/>
  </Style>
  <Style Selector="Border.pill.cat-ship">
    <Setter Property="Background" Value="#2E26C6DA"/>
    <Setter Property="BorderBrush" Value="#6126C6DA"/>
  </Style>
  <Style Selector="Border.pill.cat-ship > TextBlock">
    <Setter Property="Foreground" Value="#FF6CDDE8"/>
  </Style>
  <Style Selector="Border.pill.cat-other">
    <Setter Property="Background" Value="#2E757575"/>
    <Setter Property="BorderBrush" Value="#61757575"/>
  </Style>
  <Style Selector="Border.pill.cat-other > TextBlock">
    <Setter Property="Foreground" Value="#FFB0B0B0"/>
  </Style>

  <!-- ============================================================
       INPUTS — TextBox / NumericUpDown
       ============================================================ -->
  <Style Selector="TextBox">
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
    <Setter Property="Padding" Value="10,7"/>
    <Setter Property="FontSize" Value="13"/>
  </Style>
  <Style Selector="TextBox /template/ Border#PART_BorderElement">
    <Setter Property="Background" Value="#0AFFFFFF"/>
    <Setter Property="BorderBrush" Value="{StaticResource BorderStrongBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
  </Style>
  <Style Selector="TextBox:pointerover /template/ Border#PART_BorderElement">
    <Setter Property="BorderBrush" Value="#47FFFFFF"/> <!-- 28% -->
  </Style>
  <Style Selector="TextBox:focus /template/ Border#PART_BorderElement">
    <Setter Property="BorderBrush" Value="{StaticResource AccentBorderBrush}"/>
    <Setter Property="Background" Value="#0FFFFFFF"/>
  </Style>

  <Style Selector="NumericUpDown">
    <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
    <Setter Property="FontSize" Value="13"/>
  </Style>

  <!-- ============================================================
       LIST ROW — apply via Classes="list-row" on a Border around
       the row contents. Visible action buttons fade in on hover.
       ============================================================ -->
  <Style Selector="Border.list-row">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="2,0,0,1"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="Padding" Value="12,9"/>
  </Style>
  <Style Selector="Border.list-row:pointerover">
    <Setter Property="Background" Value="#08FFFFFF"/>
  </Style>
  <Style Selector="Border.list-row:pointerover #PART_RowActions">
    <Setter Property="Opacity" Value="1"/>
  </Style>
  <Style Selector="Border.list-row #PART_RowActions">
    <Setter Property="Opacity" Value="0"/>
    <Setter Property="Transitions">
      <Transitions>
        <DoubleTransition Property="Opacity" Duration="0:0:0.12"/>
      </Transitions>
    </Setter>
  </Style>

</Styles>
```

- [ ] **Step 2: Register Components.axaml in `App.axaml`**

Open `CubicOdysseyVault.UI/App.axaml`. Inside `<Application.Styles>`, add a `<StyleInclude>` line after the FluentTheme:

```xml
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="/Themes/Components.axaml"/>
</Application.Styles>
```

Leave `<Application.Resources>` unchanged.

- [ ] **Step 3: Build to confirm Components.axaml parses**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded. Pay attention to any XAML parser errors — Avalonia is strict about selector syntax. If a `/template/` selector fails, comment that one out and document it as TODO so the rest can build.

- [ ] **Step 4: Run the app to confirm no runtime crash**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: App launches. Window appears. No view changes are visible yet — the new styles exist but no `Classes="..."` consumers exist. Close the app.

- [ ] **Step 5: Run tests**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 141 tests pass.

- [ ] **Step 6: Commit**

```bash
git add CubicOdysseyVault.UI/Themes/Components.axaml \
        CubicOdysseyVault.UI/App.axaml
git commit -m "feat(ui): add Components.axaml with reusable styles"
```

---

## Phase B — MainWindow refit (7 tasks)

### Task 3: Add `TotalSnapshotCount` and `TotalDiskUsedBytes` to `MainWindowViewModel`

The new sidebar Storage stat block (Task 5) needs these two derived values. TDD this one — it's the only ViewModel logic change in the cleanup.

**Files:**
- Create: `CubicOdysseyVault.Tests/MainWindowViewModelStorageStatsTests.cs`
- Modify: `CubicOdysseyVault.UI/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Write the failing tests**

Create `CubicOdysseyVault.Tests/MainWindowViewModelStorageStatsTests.cs`:

```csharp
using System.Collections.Generic;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.UI.ViewModels;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class MainWindowViewModelStorageStatsTests
{
    [Fact]
    public void ComputeStorageStats_EmptyInputs_ReturnsZero()
    {
        var (count, bytes) = MainWindowViewModel.ComputeStorageStats(
            new List<IReadOnlyList<Snapshot>>());
        Assert.Equal(0, count);
        Assert.Equal(0L, bytes);
    }

    [Fact]
    public void ComputeStorageStats_MultipleSlots_SumsAll()
    {
        var slotA = new List<Snapshot>
        {
            new() { TotalBytes = 1000 },
            new() { TotalBytes = 2500 },
        };
        var slotB = new List<Snapshot>
        {
            new() { TotalBytes = 500 },
        };
        var (count, bytes) = MainWindowViewModel.ComputeStorageStats(
            new List<IReadOnlyList<Snapshot>> { slotA, slotB });
        Assert.Equal(3, count);
        Assert.Equal(4000L, bytes);
    }

    [Fact]
    public void FormatBytes_RendersSensibly()
    {
        Assert.Equal("0 B",     MainWindowViewModel.FormatBytes(0));
        Assert.Equal("512 B",   MainWindowViewModel.FormatBytes(512));
        Assert.Equal("1.5 KB",  MainWindowViewModel.FormatBytes(1536));
        Assert.Equal("2.0 MB",  MainWindowViewModel.FormatBytes(2 * 1024 * 1024));
        Assert.Equal("1.5 GB",  MainWindowViewModel.FormatBytes((long)(1.5 * 1024 * 1024 * 1024)));
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test CubicOdysseyVault.sln --filter FullyQualifiedName~MainWindowViewModelStorageStatsTests
```

Expected: 3 tests fail with "ComputeStorageStats does not exist" or compile error.

- [ ] **Step 3: Add the static helpers and observable properties to `MainWindowViewModel`**

Open `CubicOdysseyVault.UI/ViewModels/MainWindowViewModel.cs`. Add two `[ObservableProperty]` fields next to the existing ones (around line 19-26):

```csharp
[ObservableProperty] private int _totalSnapshotCount;
[ObservableProperty] private long _totalDiskUsedBytes;
[ObservableProperty] private string _totalDiskUsedText = "0 B";
```

Add these two static methods at the bottom of the class (above the closing brace):

```csharp
public static (int count, long bytes) ComputeStorageStats(
    IEnumerable<IReadOnlyList<Snapshot>> snapshotLists)
{
    int count = 0;
    long bytes = 0;
    foreach (var list in snapshotLists)
    {
        foreach (var s in list)
        {
            count++;
            bytes += s.TotalBytes;
        }
    }
    return (count, bytes);
}

public static string FormatBytes(long bytes)
{
    if (bytes < 1024) return $"{bytes} B";
    double kb = bytes / 1024.0;
    if (kb < 1024) return $"{kb:0.#} KB";
    double mb = kb / 1024.0;
    if (mb < 1024) return $"{mb:0.0} MB";
    double gb = mb / 1024.0;
    return $"{gb:0.0} GB";
}
```

Update the property when discovery refreshes. In `RefreshDiscoveryAsync`, after the `foreach (var s in result.Sources) DiscoveredSources.Add(s);` line (around line 69), add:

```csharp
RecomputeStorageStats();
```

And add this new private method below the existing private helpers:

```csharp
private void RecomputeStorageStats()
{
    var lists = new List<IReadOnlyList<Snapshot>>();
    foreach (var user in SteamUsers)
    {
        foreach (var slot in user.Slots)
            lists.Add(slot.Snapshots.Select(s => s.Snapshot).ToList());
        foreach (var acct in user.Accounts)
            lists.Add(acct.Snapshots.Select(s => s.Snapshot).ToList());
    }
    var (count, bytes) = ComputeStorageStats(lists);
    TotalSnapshotCount = count;
    TotalDiskUsedBytes = bytes;
    TotalDiskUsedText = FormatBytes(bytes);
}
```

NOTE: This assumes `SaveAccountViewModel` exposes a `Snapshots` ObservableCollection of `SnapshotViewModel` similar to `SaveSlotViewModel`. If that doesn't exist on `SaveAccountViewModel`, drop the `foreach (var acct ...)` loop — slot snapshots alone are most of the volume.

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test CubicOdysseyVault.sln --filter FullyQualifiedName~MainWindowViewModelStorageStatsTests
```

Expected: 3 tests pass.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass (141 existing + 3 new).

- [ ] **Step 6: Commit**

```bash
git add CubicOdysseyVault.UI/ViewModels/MainWindowViewModel.cs \
        CubicOdysseyVault.Tests/MainWindowViewModelStorageStatsTests.cs
git commit -m "feat(ui): add storage stats derived properties for sidebar"
```

---

### Task 4: MainWindow toolbar refit

Replace the toolbar's bare buttons with `Classes="ghost"`, swap the `FontSize="11"` "Scanning..." TextBlock for `Classes="caption"`, and use `{StaticResource SpaceMd}` for spacing. App-icon and app-name text added at the start of the row.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/MainWindow.axaml` — the toolbar `<Border DockPanel.Dock="Top">` block (lines ~14-37)

- [ ] **Step 1: Replace the toolbar block**

In `MainWindow.axaml`, replace lines 14–37 (the toolbar `<Border>` + its `<StackPanel>`) with:

```xml
<!-- Toolbar -->
<Border DockPanel.Dock="Top"
        Background="{StaticResource SidebarBackgroundBrush}"
        BorderBrush="{StaticResource BorderSubtleBrush}"
        BorderThickness="0,0,0,1"
        Padding="{StaticResource PadLg}">
    <DockPanel LastChildFill="False">
        <Image DockPanel.Dock="Left"
               Source="/Assets/icon.png"
               Width="24" Height="24"
               Margin="0,0,8,0"/>
        <TextBlock DockPanel.Dock="Left"
                   Text="Cubic Odyssey Vault"
                   Classes="body-emph"
                   VerticalAlignment="Center"/>
        <TextBlock DockPanel.Dock="Left"
                   Text="Scanning..."
                   Classes="caption"
                   VerticalAlignment="Center"
                   Margin="12,0,0,0"
                   IsVisible="{Binding IsDiscovering}"/>

        <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" Spacing="{StaticResource SpaceSm}">
            <Button Content="Refresh"
                    Classes="ghost"
                    Command="{Binding RefreshDiscoveryCommand}"
                    IsEnabled="{Binding !IsDiscovering}"/>
            <Button Content="Settings"
                    Classes="ghost"
                    Command="{Binding OpenSettingsCommand}"/>
            <Button Content="Open backup folder"
                    Classes="ghost"
                    Command="{Binding OpenBackupFolderCommand}"/>
        </StackPanel>
    </DockPanel>
</Border>
```

- [ ] **Step 2: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 3: Run app to visually verify**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Toolbar shows the app icon + name on the left, three transparent ghost buttons on the right. No filled blue buttons. Hover over a button: subtle white-ish background appears. Close the app.

- [ ] **Step 4: Commit**

```bash
git add CubicOdysseyVault.UI/Views/MainWindow.axaml
git commit -m "feat(ui): polish MainWindow toolbar with ghost buttons + app branding"
```

---

### Task 5: MainWindow sidebar refit + Watcher/Storage stat blocks

Replace the sidebar's `FontSize="10" FontWeight="SemiBold"` headers with `Classes="label"`, add the Watcher and Storage stat blocks at the bottom, and use the new spacing tokens.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/MainWindow.axaml` — the sidebar `<Border DockPanel.Dock="Left">` block (lines ~50-89)

- [ ] **Step 1: Replace the sidebar block**

In `MainWindow.axaml`, replace lines 50–89 (the sidebar `<Border>`) with:

```xml
<!-- Sidebar -->
<Border DockPanel.Dock="Left"
        Width="240"
        Background="{StaticResource SidebarBackgroundBrush}"
        BorderBrush="{StaticResource BorderSubtleBrush}"
        BorderThickness="0,0,1,0"
        Padding="{StaticResource PadLg}">
    <DockPanel>

        <!-- Watcher + Storage at the bottom -->
        <StackPanel DockPanel.Dock="Bottom" Spacing="{StaticResource SpaceMd}" Margin="0,16,0,0">
            <Border Background="{StaticResource BorderSubtleBrush}" Height="1"/>

            <StackPanel Spacing="{StaticResource SpaceXs}">
                <TextBlock Text="WATCHER" Classes="label"/>
                <Grid ColumnDefinitions="Auto,*,Auto">
                    <Ellipse Grid.Column="0" Width="8" Height="8"
                             Fill="{StaticResource HealthHealthyBrush}"
                             VerticalAlignment="Center"
                             IsVisible="{Binding IsWatcherEnabled}"/>
                    <Ellipse Grid.Column="0" Width="8" Height="8"
                             Fill="{StaticResource TextDisabledBrush}"
                             VerticalAlignment="Center"
                             IsVisible="{Binding !IsWatcherEnabled}"/>
                    <TextBlock Grid.Column="1" Classes="body-sm"
                               Text="Auto-snapshot" Margin="8,0,0,0"
                               VerticalAlignment="Center"/>
                    <TextBlock Grid.Column="2" Classes="numeric"
                               FontSize="11"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               Text="ON" VerticalAlignment="Center"
                               IsVisible="{Binding IsWatcherEnabled}"/>
                    <TextBlock Grid.Column="2" Classes="numeric"
                               FontSize="11"
                               Foreground="{StaticResource TextDisabledBrush}"
                               Text="OFF" VerticalAlignment="Center"
                               IsVisible="{Binding !IsWatcherEnabled}"/>
                </Grid>
                <Grid ColumnDefinitions="*,Auto" IsVisible="{Binding IsWatcherEnabled}">
                    <TextBlock Grid.Column="0" Classes="body-sm" Text="Debounce"/>
                    <TextBlock Grid.Column="1" Classes="numeric" FontSize="11"
                               Foreground="{StaticResource TextSecondaryBrush}">
                        <Run Text="{Binding WatcherDebounceSeconds}"/><Run Text="s"/>
                    </TextBlock>
                </Grid>
            </StackPanel>

            <Border Background="{StaticResource BorderSubtleBrush}" Height="1"/>

            <StackPanel Spacing="{StaticResource SpaceXs}">
                <TextBlock Text="STORAGE" Classes="label"/>
                <Grid ColumnDefinitions="*,Auto">
                    <TextBlock Grid.Column="0" Classes="body-sm" Text="Snapshots"/>
                    <TextBlock Grid.Column="1" Classes="numeric" FontSize="11"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               Text="{Binding TotalSnapshotCount}"/>
                </Grid>
                <Grid ColumnDefinitions="*,Auto">
                    <TextBlock Grid.Column="0" Classes="body-sm" Text="Used"/>
                    <TextBlock Grid.Column="1" Classes="numeric" FontSize="11"
                               Foreground="{StaticResource TextPrimaryBrush}"
                               Text="{Binding TotalDiskUsedText}"/>
                </Grid>
            </StackPanel>
        </StackPanel>

        <!-- User list at the top -->
        <TextBlock DockPanel.Dock="Top"
                   Text="STEAM USERS"
                   Classes="label"
                   Margin="0,0,0,8"/>
        <TextBlock DockPanel.Dock="Top"
                   Text="(none discovered)"
                   Classes="body-sm"
                   Margin="0,0,0,8"
                   IsVisible="{Binding ShowEmptyState}"/>
        <ListBox ItemsSource="{Binding SteamUsers}"
                 SelectedItem="{Binding SelectedSteamUser}"
                 Background="Transparent"
                 BorderThickness="0">
            <ListBox.ItemTemplate>
                <DataTemplate x:DataType="vm:SteamUserViewModel">
                    <StackPanel Spacing="2" Margin="0,4">
                        <TextBlock Text="{Binding SteamId32}" Classes="body-emph"/>
                        <TextBlock Classes="body-sm">
                            <Run Text="{Binding SlotCount}"/>
                            <Run Text=" slots"/>
                        </TextBlock>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </DockPanel>
</Border>
```

- [ ] **Step 2: Add `IsWatcherEnabled` and `WatcherDebounceSeconds` properties to `MainWindowViewModel`**

The sidebar bindings reference `IsWatcherEnabled` and `WatcherDebounceSeconds` which need to be exposed. Open `CubicOdysseyVault.UI/ViewModels/MainWindowViewModel.cs` and add next to the existing `[ObservableProperty]` fields:

```csharp
[ObservableProperty] private bool _isWatcherEnabled;
[ObservableProperty] private int _watcherDebounceSeconds;
```

In `ApplySettings(AppSettings updated)` (around line 252), at the end of the method add:

```csharp
IsWatcherEnabled = _settings.WatcherEnabled;
WatcherDebounceSeconds = _settings.WatcherDebounceSeconds;
```

In the constructor (`MainWindowViewModel()` around line 43), at the end of the body add the same two lines:

```csharp
IsWatcherEnabled = _settings.WatcherEnabled;
WatcherDebounceSeconds = _settings.WatcherDebounceSeconds;
```

- [ ] **Step 3: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 4: Run app and visually verify**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Sidebar shows uppercase "STEAM USERS" eyebrow label at the top, user list below it. At the bottom: "WATCHER" block with green dot + "Auto-snapshot ON" + "Debounce Ns" line; "STORAGE" block with snapshot count and disk used. Close the app.

- [ ] **Step 5: Run tests**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass.

- [ ] **Step 6: Commit**

```bash
git add CubicOdysseyVault.UI/Views/MainWindow.axaml \
        CubicOdysseyVault.UI/ViewModels/MainWindowViewModel.cs
git commit -m "feat(ui): polish sidebar with eyebrow labels + watcher/storage stat blocks"
```

---

### Task 6: MainWindow account-level card refit

Replace the inline-styled `<Border>` for the account-level row with `Classes="card"` and use eyebrow + body-emph + body-sm typography. Move the "Back up now" button to use `Classes="primary"`.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/MainWindow.axaml` — the `<ItemsControl DockPanel.Dock="Top" ItemsSource="{Binding SelectedSteamUser.Accounts}">` block (lines ~223-259)

- [ ] **Step 1: Replace the account-level item template**

In `MainWindow.axaml`, find the `<ItemsControl DockPanel.Dock="Top" ...>` for accounts. Replace its `<DataTemplate>` body (the whole `<Border>` inside) with:

```xml
<DataTemplate x:DataType="vm:SaveAccountViewModel">
    <Border Classes="card" Padding="{StaticResource PadLg}" Margin="0,0,0,4">
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Grid.Column="0" Spacing="3">
                <TextBlock Text="ACCOUNT-LEVEL DATA" Classes="label"/>
                <TextBlock Classes="body">
                    <Run Text="{Binding FileCount}"/><Run Text=" files · "/><Run Text="{Binding FormattedSize}"/><Run Text=" · "/><Run Text="{Binding SourceLabel}"/>
                </TextBlock>
                <TextBlock Classes="body-sm">
                    <Run Text="{Binding LastWriteText}"/><Run Text=" · "/><Run Text="{Binding LastSnapshotText}"/>
                </TextBlock>
                <TextBlock Text="{Binding BackupStatus}"
                           Classes="body-sm"
                           Foreground="{StaticResource AccentBrush}"
                           IsVisible="{Binding BackupStatus, Converter={x:Static ObjectConverters.IsNotNull}}"/>
            </StackPanel>
            <Button Grid.Column="1"
                    Content="Back up now"
                    Classes="primary"
                    VerticalAlignment="Center"
                    Command="{Binding BackUpNowCommand}"/>
        </Grid>
    </Border>
</DataTemplate>
```

- [ ] **Step 2: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 3: Run and verify**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Account-level card has the gradient background and the "Back up now" button is filled red. Close the app.

- [ ] **Step 4: Commit**

```bash
git add CubicOdysseyVault.UI/Views/MainWindow.axaml
git commit -m "feat(ui): polish account-level card with shared card style"
```

---

### Task 7: MainWindow slot card refit

Use `Border.card` with `Classes.selected` bound to the parent `ListBoxItem.IsSelected`. Replace the per-card "Back up now" button (cleanup — it's redundant with the detail panel's button). Replace the four `<Ellipse>` health dots with a single `Border.pill` per health state. Add the voxel notch (an absolutely-positioned `Path`) visible only when selected.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/MainWindow.axaml` — the slot grid `<ListBox>` and its item template (lines ~263-344)

- [ ] **Step 1: Replace the slot grid block**

In `MainWindow.axaml`, replace the `<ScrollViewer>` containing the `<ListBox ItemsSource="{Binding SelectedSteamUser.Slots}">` (lines 261-345) with:

```xml
<ScrollViewer HorizontalScrollBarVisibility="Disabled"
              VerticalScrollBarVisibility="Auto">
    <ListBox ItemsSource="{Binding SelectedSteamUser.Slots}"
             SelectedItem="{Binding SelectedSlot}"
             Background="Transparent"
             BorderThickness="0">
        <ListBox.Styles>
            <Style Selector="ListBoxItem">
                <Setter Property="Padding" Value="0"/>
                <Setter Property="Margin" Value="0,0,12,12"/>
            </Style>
            <Style Selector="ListBoxItem /template/ ContentPresenter#PART_ContentPresenter">
                <Setter Property="Background" Value="Transparent"/>
                <Setter Property="BorderThickness" Value="0"/>
                <Setter Property="Padding" Value="0"/>
            </Style>
            <!-- Hover lift on slot cards (only inside this ListBox) -->
            <Style Selector="ListBoxItem:pointerover Border.card">
                <Setter Property="BoxShadow" Value="0 4 14 #80000000"/>
                <Setter Property="BorderBrush" Value="#26FFFFFF"/>
            </Style>
        </ListBox.Styles>
        <ListBox.ItemsPanel>
            <ItemsPanelTemplate>
                <WrapPanel Orientation="Horizontal"/>
            </ItemsPanelTemplate>
        </ListBox.ItemsPanel>
        <ListBox.ItemTemplate>
            <DataTemplate x:DataType="vm:SaveSlotViewModel">
                <Border Width="220" Height="200"
                        Classes="card"
                        Classes.selected="{Binding $parent[ListBoxItem].IsSelected}">
                    <Grid>
                        <DockPanel>
                            <!-- Thumbnail -->
                            <Border DockPanel.Dock="Top" Height="108"
                                    CornerRadius="8,8,0,0"
                                    BorderBrush="{StaticResource BorderSubtleBrush}"
                                    BorderThickness="0,0,0,1"
                                    ClipToBounds="True"
                                    Background="{StaticResource SidebarBackgroundBrush}">
                                <Image Source="{Binding Screenshot}" Stretch="UniformToFill"/>
                            </Border>
                            <!-- Body -->
                            <StackPanel Spacing="{StaticResource SpaceXs}"
                                        Margin="{StaticResource PadMd}"
                                        VerticalAlignment="Top">
                                <TextBlock Classes="title">
                                    <Run Text="Slot "/><Run Text="{Binding SlotName}"/>
                                </TextBlock>
                                <TextBlock Classes="body-sm">
                                    <Run Text="{Binding FileCount}"/><Run Text=" files"/>
                                </TextBlock>
                                <Grid ColumnDefinitions="Auto,*,Auto" Margin="0,4,0,0">
                                    <Border Grid.Column="0" Classes="pill health-ok"
                                            IsVisible="{Binding IsHealthHealthy}">
                                        <TextBlock Text="HEALTHY"/>
                                    </Border>
                                    <Border Grid.Column="0" Classes="pill health-sus"
                                            IsVisible="{Binding IsHealthSuspicious}">
                                        <TextBlock Text="SUSPECT"/>
                                    </Border>
                                    <Border Grid.Column="0" Classes="pill health-bad"
                                            IsVisible="{Binding IsHealthCorrupted}">
                                        <TextBlock Text="CORRUPTED"/>
                                    </Border>
                                    <TextBlock Grid.Column="2" Classes="caption"
                                               VerticalAlignment="Center"
                                               Text="{Binding LastSnapshotText}"/>
                                </Grid>
                            </StackPanel>
                        </DockPanel>
                        <!-- Voxel notch — top-right, only when selected -->
                        <Path Data="M 0 0 L 16 0 L 16 16 Z"
                              Fill="{StaticResource AccentBrush}"
                              HorizontalAlignment="Right"
                              VerticalAlignment="Top"
                              IsVisible="{Binding $parent[ListBoxItem].IsSelected}"/>
                    </Grid>
                </Border>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</ScrollViewer>
```

Card dimensions shrink from 240×232 to 220×200 because the per-card "Back up now" button is removed (the detail panel's button covers that action). The thumbnail height grows proportionally.

- [ ] **Step 2: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 3: Run and verify**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Slot cards have the gradient background, rounded corners, subtle shadow. Hovering one shows the brighter border. Clicking selects: red border + glow + small red voxel notch in the top-right. The "Back up now" button on the card is gone — that action lives in the detail panel only. Health is shown as a colored pill ("HEALTHY"/"SUSPECT"/"CORRUPTED") instead of a dot. Close the app.

- [ ] **Step 4: Run tests**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Views/MainWindow.axaml
git commit -m "feat(ui): polish slot cards with selected state + voxel notch + health pill"
```

---

### Task 8: MainWindow detail panel refit

Widen from 360 to 400px. Replace the existing health badges (Border + TextBlock combos) with `Border.pill.health-*`. Use `Classes="heading"` for the slot title, `Classes="body"` / `Classes="caption"` for metadata. Buttons become `Classes="primary"` (Back up now) and `Classes="secondary"` (Inspect save data).

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/MainWindow.axaml` — the detail panel `<Border DockPanel.Dock="Right">` block (lines ~92-218)

- [ ] **Step 1: Replace the detail panel block**

In `MainWindow.axaml`, replace lines 92–218 (`<Border DockPanel.Dock="Right">` and its full content) with:

```xml
<!-- Detail panel (right; visible when slot is selected) -->
<Border DockPanel.Dock="Right"
        Width="400"
        Background="{StaticResource SidebarBackgroundBrush}"
        BorderBrush="{StaticResource BorderSubtleBrush}"
        BorderThickness="1,0,0,0"
        Padding="{StaticResource PadXl}"
        IsVisible="{Binding SelectedSlot, Converter={x:Static ObjectConverters.IsNotNull}}">
    <ScrollViewer DataContext="{Binding SelectedSlot}" x:DataType="vm:SaveSlotViewModel">
        <StackPanel Spacing="{StaticResource SpaceLg}">
            <!-- Hero screenshot -->
            <Border CornerRadius="{StaticResource RadiusLg}"
                    BorderBrush="{StaticResource BorderDefaultBrush}"
                    BorderThickness="1"
                    ClipToBounds="True"
                    Background="{StaticResource SidebarBackgroundBrush}"
                    BoxShadow="0 4 14 #80000000">
                <Image Source="{Binding Screenshot}" Stretch="Uniform" MaxHeight="200"/>
            </Border>

            <!-- Title + metadata -->
            <StackPanel Spacing="{StaticResource SpaceXs}">
                <TextBlock Classes="label">
                    <Run Text="SLOT "/><Run Text="{Binding SlotName}"/><Run Text=" · ACCT "/><Run Text="{Binding AccountFolderName}"/>
                </TextBlock>
                <Grid ColumnDefinitions="Auto,Auto,*">
                    <TextBlock Grid.Column="0" Classes="heading" Text="{Binding SourceLabel}" Margin="0,0,8,0"/>
                    <Border Grid.Column="1" Classes="pill health-ok"
                            VerticalAlignment="Center"
                            IsVisible="{Binding IsHealthHealthy}">
                        <TextBlock Text="HEALTHY"/>
                    </Border>
                    <Border Grid.Column="1" Classes="pill health-sus"
                            VerticalAlignment="Center"
                            IsVisible="{Binding IsHealthSuspicious}">
                        <TextBlock Text="SUSPECT"/>
                    </Border>
                    <Border Grid.Column="1" Classes="pill health-bad"
                            VerticalAlignment="Center"
                            IsVisible="{Binding IsHealthCorrupted}">
                        <TextBlock Text="CORRUPTED"/>
                    </Border>
                </Grid>
                <TextBlock Classes="body-sm">
                    <Run Text="{Binding FileCount}"/><Run Text=" files · "/><Run Text="{Binding FormattedSize}"/>
                </TextBlock>
                <TextBlock Classes="body-sm" Text="{Binding LastWriteText}"/>
            </StackPanel>

            <!-- Action row -->
            <StackPanel Orientation="Horizontal" Spacing="{StaticResource SpaceSm}">
                <Button Content="Back up now"
                        Classes="primary"
                        Command="{Binding BackUpNowCommand}"/>
                <Button Content="Inspect save data..."
                        Classes="secondary"
                        Command="{Binding $parent[Window].((vm:MainWindowViewModel)DataContext).InspectSelectedSlotCommand}"/>
            </StackPanel>
            <TextBlock Text="{Binding BackupStatus}"
                       Classes="body-sm"
                       TextWrapping="Wrap"
                       IsVisible="{Binding BackupStatus, Converter={x:Static ObjectConverters.IsNotNull}}"/>

            <!-- Snapshot history -->
            <StackPanel Spacing="{StaticResource SpaceSm}">
                <Grid ColumnDefinitions="*,Auto">
                    <TextBlock Grid.Column="0" Text="SNAPSHOT HISTORY" Classes="label"/>
                    <TextBlock Grid.Column="1" Classes="numeric" FontSize="11"
                               Foreground="{StaticResource TextSecondaryBrush}"
                               Text="{Binding SnapshotCount}"/>
                </Grid>
                <TextBlock Text="No snapshots yet. Click Back up now to create one."
                           Classes="body-sm"
                           TextWrapping="Wrap"
                           IsVisible="{Binding !SnapshotCount}"/>

                <!-- Snapshot rows live here — replaced in Task 9 -->
                <ItemsControl ItemsSource="{Binding Snapshots}"
                              x:Name="SnapshotHistoryItems"/>
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</Border>
```

Note: this leaves `<ItemsControl x:Name="SnapshotHistoryItems"/>` empty — Task 9 fills it back in.

- [ ] **Step 2: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 3: Run and verify**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Detail panel is wider (400px). Hero screenshot has soft shadow + radius. Health is a pill. "Back up now" is filled red, "Inspect save data..." has the secondary outline. Snapshot history is empty (will populate in Task 9). Close the app.

- [ ] **Step 4: Commit**

```bash
git add CubicOdysseyVault.UI/Views/MainWindow.axaml
git commit -m "feat(ui): polish detail panel — wider, hero frame, pill badges, semantic buttons"
```

---

### Task 9: MainWindow snapshot history rows

Restore the snapshot history with the new `Border.list-row` style — pills for health and trigger, action buttons that fade in on hover.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/MainWindow.axaml` — replace the empty `<ItemsControl x:Name="SnapshotHistoryItems"/>` from Task 8 with the full template

- [ ] **Step 1: Replace the empty `<ItemsControl>` with the new history template**

In `MainWindow.axaml`, find `<ItemsControl ItemsSource="{Binding Snapshots}" x:Name="SnapshotHistoryItems"/>` and replace with:

```xml
<ItemsControl ItemsSource="{Binding Snapshots}">
    <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="vm:SnapshotViewModel">
            <Border Classes="list-row" BorderBrush="{StaticResource BorderSubtleBrush}">
                <Grid ColumnDefinitions="Auto,Auto,*,Auto">

                    <!-- Health pill -->
                    <Border Grid.Column="0" Classes="pill health-ok"
                            VerticalAlignment="Center"
                            IsVisible="{Binding IsTriggerManual}">
                        <TextBlock Text="HEALTHY"/>
                    </Border>

                    <!-- Trigger pill -->
                    <Border Grid.Column="1" Classes="pill trigger-manual"
                            Margin="6,0,0,0"
                            IsVisible="{Binding IsTriggerManual}">
                        <TextBlock Text="MANUAL"/>
                    </Border>
                    <Border Grid.Column="1" Classes="pill trigger-auto"
                            Margin="6,0,0,0"
                            IsVisible="{Binding IsTriggerAuto}">
                        <TextBlock Text="AUTO"/>
                    </Border>
                    <Border Grid.Column="1" Classes="pill trigger-pre"
                            Margin="6,0,0,0"
                            IsVisible="{Binding IsTriggerPreRestore}">
                        <TextBlock Text="PRE-RESTORE"/>
                    </Border>

                    <!-- Time + size -->
                    <StackPanel Grid.Column="2" Margin="10,0,0,0" VerticalAlignment="Center">
                        <TextBlock Classes="body-emph" Text="{Binding CapturedAtText}"/>
                        <StackPanel Orientation="Horizontal" Spacing="6">
                            <TextBlock Classes="caption" Text="{Binding FormattedSize}"/>
                            <TextBlock Classes="caption" Text="·"/>
                            <TextBlock Classes="caption" Text="{Binding HealthLabel}"/>
                            <TextBlock Classes="caption" Foreground="{StaticResource AccentBrush}"
                                       FontStyle="Italic"
                                       Text="{Binding Tag}"
                                       IsVisible="{Binding Tag, Converter={x:Static ObjectConverters.IsNotNull}}"/>
                        </StackPanel>
                    </StackPanel>

                    <!-- Action buttons (fade in on hover via list-row style) -->
                    <StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="2"
                                x:Name="PART_RowActions"
                                VerticalAlignment="Center">
                        <Button Classes="icon" Content="↺" ToolTip.Tip="Restore"
                                Command="{Binding RestoreCommand}"/>
                        <Button Classes="icon" Content="🏷" ToolTip.Tip="Tag"
                                Command="{Binding EditTagCommand}"/>
                        <Button Classes="icon" Content="🗑" ToolTip.Tip="Delete"
                                Command="{Binding DeleteCommand}"/>
                    </StackPanel>
                </Grid>
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

Note the `x:Name="PART_RowActions"` on the action StackPanel — this is what the `Border.list-row:pointerover #PART_RowActions` selector targets to fade them in.

- [ ] **Step 2: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 3: Run and verify**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Snapshot history rows show health pill + trigger pill + time + size · health · tag (if any). Action buttons (↺ 🏷 🗑) are invisible by default; hovering a row fades them in. Close the app.

- [ ] **Step 4: Run tests**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Views/MainWindow.axaml
git commit -m "feat(ui): polish snapshot history rows with pills + fade-in actions"
```

---

## Phase C — Dialogs (6 tasks)

Each dialog applies tokens + component styles. No layout restructuring — the goal is consistent visual language across all six.

### Task 10: SettingsDialog refit

Apply `Classes="label"` to section headers, `Classes="body"`/`"body-sm"` to descriptions, `Classes="primary"` and `"secondary"` to footer buttons.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/SettingsDialog.axaml`

- [ ] **Step 1: Read the current file**

Read `CubicOdysseyVault.UI/Views/SettingsDialog.axaml` to understand its structure. The dialog is 560×700 with: backup root TextBox + Browse, game install path TextBox + Browse, manual sources ListBox + Add/Remove, watcher CheckBox + debounce NumericUpDown, retention NumericUpDowns, Cancel/Save footer.

- [ ] **Step 2: Apply class-based styling**

For each section header `TextBlock` in the dialog (currently styled with inline `FontSize="11" FontWeight="SemiBold" Foreground="{StaticResource TextSecondaryBrush}"`), replace those three properties with `Classes="label"`. Section headers include: "Backup root", "Game install path", "Manual save sources", "File watcher", "Retention", or whatever the current section labels are.

For descriptive helper text (currently styled with `FontSize="11" Foreground="{StaticResource TextSecondaryBrush}" TextWrapping="Wrap"`), replace with `Classes="body-sm"`.

For the dialog title (currently styled with `FontSize="20" FontWeight="SemiBold"`), replace with `Classes="heading"` and adjust margin if needed.

For the Cancel button, add `Classes="secondary"`.
For the Save button, add `Classes="primary"`.
For Browse / Add / Remove buttons, add `Classes="secondary"`.

For padding/margin values, use the spacing scale (4/8/12/16/24/32/48):
- **Uniform `Padding`/`Margin`** can use the resource: `Padding="{StaticResource PadMd}"` (resource type is Thickness).
- **Non-uniform `Margin` with comma-separated values** must use inline literals because Avalonia's XAML parser doesn't allow `{StaticResource}` inside comma lists. Example: `Margin="0,0,0,12"` (where 12 = SpaceMd value).
- **`Spacing` on StackPanel** (Double type): `Spacing="{StaticResource SpaceMd}"`.

For container Borders styled with `Background="{StaticResource SidebarBlockBrush}" BorderBrush="{StaticResource SeparatorBrush}" BorderThickness="1" CornerRadius="4"`, replace with `Classes="card"` and remove the four explicit properties.

- [ ] **Step 3: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 4: Run and visually verify the Settings dialog**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Click "Settings" in the toolbar. Dialog opens. Expected: section headers are uppercase eyebrow labels in muted color; inputs have the new BorderStrong outline + accent focus glow; footer has filled-red Save and outlined Cancel. Close the dialog and the app.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Views/SettingsDialog.axaml
git commit -m "feat(ui): polish SettingsDialog with shared tokens and component styles"
```

---

### Task 11: OnboardingDialog refit

OnboardingDialog has two steps. Step 1 is a welcome screen — title + intro text + detected sources summary. Step 2 reuses the SettingsDialog form. Apply `Classes="display"` to Step 1's heading, `Classes="body"` to its paragraph, `Classes="card"` to the detected sources block, and `Classes="primary"`/`"secondary"` to footer buttons (Back/Next/Get started/Cancel).

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/OnboardingDialog.axaml`

- [ ] **Step 1: Read the current file**

Read `CubicOdysseyVault.UI/Views/OnboardingDialog.axaml` to confirm structure (640×620, two `<StackPanel IsVisible=...>` blocks for Step 1 and Step 2).

- [ ] **Step 2: Apply class-based styling**

For Step 1's main heading TextBlock (currently `FontSize="22" FontWeight="SemiBold"` or similar), replace with `Classes="display"`.

For Step 1's body paragraph, replace inline FontSize with `Classes="body"`.

For the "detected sources" Border (currently styled inline as a card-like block), replace its inline properties with `Classes="card"` + `Padding="{StaticResource PadLg}"`.

For the form labels in Step 2, apply `Classes="label"` (these duplicate Settings section headers).

For the footer buttons:
- "Cancel" → `Classes="secondary"`
- "Back" → `Classes="secondary"`
- "Next" → `Classes="primary"`
- "Get started" → `Classes="primary"`

Replace explicit pixel margins/paddings with the spacing scale: uniform `Padding`/`Margin` → `{StaticResource Pad*}` (Thickness); StackPanel `Spacing` → `{StaticResource Space*}` (Double); non-uniform `Margin="A,B,C,D"` stays inline. Scale values: 4 / 8 / 12 / 16 / 24 / 32 / 48.

- [ ] **Step 3: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 4: Verify by triggering onboarding**

To trigger onboarding, edit `%APPDATA%/CubicOdysseyVault/settings.json` to set `"HasCompletedOnboarding": false`, then:

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Expected: Onboarding dialog appears on launch. Step 1 has a large display heading, body paragraph, card-styled detected-sources block, and "Cancel"/"Get started" buttons in the new styles. Click "Get started" without changing anything — moves to Step 2 with form. "Back"/"Get started" buttons styled. Close the dialog (it sets HasCompletedOnboarding back to true). Close the app.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Views/OnboardingDialog.axaml
git commit -m "feat(ui): polish OnboardingDialog with shared tokens and component styles"
```

---

### Task 12: RestoreConfirmDialog refit

Apply hero frame styling to the screenshot, replace inline pill borders with `Classes="pill"`, swap the "game running" warning to a `pill.health-sus`, and apply `Classes="primary"`/`"secondary"` to footer.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml`

- [ ] **Step 1: Read the current file**

Read `CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml`. Note where the Image element is (the screenshot block), where the trigger/source/health badges are, and where the "game running" warning lives.

- [ ] **Step 2: Apply class-based styling**

Wrap the screenshot Image in a Border with these properties:
```xml
<Border CornerRadius="{StaticResource RadiusLg}"
        BorderBrush="{StaticResource BorderDefaultBrush}"
        BorderThickness="1"
        ClipToBounds="True"
        BoxShadow="0 4 14 #80000000">
    <Image .../>
</Border>
```

Replace each inline `<Border CornerRadius="2" Padding="5,1" Background="{StaticResource HealthHealthyBrush}">` (or similar) with `<Border Classes="pill health-ok">` containing just `<TextBlock Text="HEALTHY"/>`.

Apply the same pattern for SUSPECT/CORRUPTED/MANUAL/AUTO/PRE-RESTORE pills.

For the "game is running" warning block (currently a Border with red-tinted background and warning text), replace with:
```xml
<Border Classes="card" Padding="{StaticResource PadMd}"
        IsVisible="{Binding IsGameRunning}">
    <StackPanel Spacing="6">
        <Border Classes="pill health-sus" HorizontalAlignment="Left">
            <TextBlock Text="GAME RUNNING"/>
        </Border>
        <TextBlock Classes="body-sm"
                   Text="Cubic Odyssey is running. Restore is blocked while the game is open."
                   TextWrapping="Wrap"/>
    </StackPanel>
</Border>
```

For the footer:
- Cancel → `Classes="secondary"`
- Restore → `Classes="primary"`

Apply `Classes="heading"` to dialog title, `Classes="label"` to any eyebrow-style labels, `Classes="body"` / `"body-sm"` to paragraph text.

- [ ] **Step 3: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 4: Verify by triggering restore**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Click a slot, hover a snapshot history row, click ↺ (restore icon). Restore confirm dialog opens. Expected: hero screenshot has shadow + radius; pills are present; Restore button is filled red, Cancel outlined. Close without restoring. Close the app.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Views/RestoreConfirmDialog.axaml
git commit -m "feat(ui): polish RestoreConfirmDialog with hero frame + pills + semantic buttons"
```

---

### Task 13: SaveInspectorDialog — Summary tab refit

The biggest dialog. Summary tab has character/timestamp section, warnings section, inventory containers, ships section. Apply tokens + component styles.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/SaveInspectorDialog.axaml` — only the Summary tab content (don't touch Files tab in this task)

- [ ] **Step 1: Read the current file**

Read `CubicOdysseyVault.UI/Views/SaveInspectorDialog.axaml`. The dialog is 1200×780 with a top header Border, bottom close-button Border, and a `<TabControl>` containing two `<TabItem>` elements: "Summary" and "Files". Locate the Summary `<TabItem>` content — typically a `<ScrollViewer>` containing a `<StackPanel>` with: character/timestamp block, warnings block, inventory container ItemsControl, ships block.

- [ ] **Step 2: Apply class-based styling to Summary tab**

For the Summary tab's section headers (character / inventory / ships), replace inline TextBlock styling with `Classes="label"`.

For the character name TextBlock, apply `Classes="title"`.

For each "container card" (the Border wrapping an inventory container or a ship section), replace inline Border properties with `Classes="card"` + `Padding="{StaticResource PadLg}"`.

For inventory item rows, the existing layout has an icon Image + name + count + category badge. Replace the category Border (currently styled with inline `Background="{StaticResource CategoryWeaponBrush}"` and a fixed CornerRadius) with `Classes="pill cat-weapon"` (or the appropriate variant — match by category enum value).

The mapping is:
- `Equipment` → `cat-equipment`
- `Weapon` → `cat-weapon`
- `Resource` → `cat-resource`
- `ShipComponent` → `cat-shipcomponent`
- `Deployable` → `cat-deployable`
- `Key` → `cat-key`
- `Ship` → `cat-ship`
- `Other` → `cat-other`

Use the existing `IsCategoryX` boolean properties on `InventoryItemViewModel` for `IsVisible` bindings (one Border per category, like the health/trigger pattern in MainWindow).

For warning text TextBlocks, apply `Classes="body-sm"` and use `Foreground="{StaticResource HealthSuspiciousBrush}"` for the amber warning color.

For spacing values, snap to the scale (4/8/12/16/24/32/48). Uniform `Padding`/`Margin` use `{StaticResource Pad*}`; StackPanel `Spacing` uses `{StaticResource Space*}`; non-uniform `Margin` stays inline (e.g. `Margin="0,0,0,12"`).

- [ ] **Step 3: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 4: Verify by opening the inspector**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Click a slot, click "Inspect save data..." in the detail panel. Inspector opens to Summary tab. Expected: section eyebrow labels, character name as title, inventory containers as cards, category pills with the new styling. Close inspector and app.

- [ ] **Step 5: Commit**

```bash
git add CubicOdysseyVault.UI/Views/SaveInspectorDialog.axaml
git commit -m "feat(ui): polish SaveInspector Summary tab with cards + pills + eyebrow labels"
```

---

### Task 14: SaveInspectorDialog — Files tab refit

Files tab has file list (left sidebar 260px) + nested Decoded/Strings/Hex tabs on the right. Apply class-based styling to file list rows, apply input styling carry-over, polish the TLV TreeView item template.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/SaveInspectorDialog.axaml` — only the Files tab content

- [ ] **Step 1: Apply styling to file list**

For the left ListBox of save files, the item template TextBlock should use `Classes="body"`. Sidebar header "Files" → `Classes="label"`. ListBox itself: `Background="{StaticResource Surface1Brush}"` (or `SidebarBackgroundBrush`).

Wait — the file list uses ListBoxItem. Apply transparent ListBoxItem styling like in Task 7 so the inner Border (if any) shows the visual:

```xml
<ListBox.Styles>
    <Style Selector="ListBoxItem">
        <Setter Property="Padding" Value="0"/>
    </Style>
    <Style Selector="ListBoxItem /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="CornerRadius" Value="{StaticResource RadiusMd}"/>
    </Style>
    <Style Selector="ListBoxItem:pointerover /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="#0DFFFFFF"/>
    </Style>
    <Style Selector="ListBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource AccentSurfaceBrush}"/>
    </Style>
</ListBox.Styles>
```

- [ ] **Step 2: Polish nested Decoded/Strings/Hex tabs**

For the TLV TreeView, look up its current `<TreeView.ItemTemplate>` and inside the recursive `TreeDataTemplate`, apply:
- Tag/Type labels → `Classes="label"`
- Value text → `Classes="numeric"` if it's a hex/numeric value, else `Classes="body-sm"`
- HexPreview text → `Classes="numeric"` with monospace

For the Strings tab's ListBox, apply `Classes="body"` or `"numeric"` depending on whether the string content is monospace.

For the Hex tab's `SelectableTextBlock`, apply `FontFamily="Cascadia Mono, Consolas, monospace"` if not already set, plus `Classes="numeric"`.

For TabControl's TabItems, add some breathing room: set `Padding="{StaticResource PadMd}"` on the TabControl and ensure tab content uses `Margin="{StaticResource PadLg}"` (uniform 16px Thickness).

- [ ] **Step 3: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 4: Verify by opening the inspector and clicking Files tab**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Click slot → Inspect → Files tab. Expected: file list has subtle hover, accent-tinted selection. Decoded tab's tree shows tag/type as eyebrow labels, value in monospace numeric style. Close.

- [ ] **Step 5: Run tests**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass.

- [ ] **Step 6: Commit**

```bash
git add CubicOdysseyVault.UI/Views/SaveInspectorDialog.axaml
git commit -m "feat(ui): polish SaveInspector Files tab — list rows + TLV tree typography"
```

---

### Task 15: TagEditDialog + DeleteConfirmDialog refit (combined)

Both dialogs are small — combine into one task. TagEditDialog has a TextBox + footer buttons. DeleteConfirmDialog has metadata + warning + footer.

**Files:**
- Modify: `CubicOdysseyVault.UI/Views/TagEditDialog.axaml`
- Modify: `CubicOdysseyVault.UI/Views/DeleteConfirmDialog.axaml`

- [ ] **Step 1: Read both files**

Read both XAML files to confirm structure.

- [ ] **Step 2: TagEditDialog refit**

Apply:
- Dialog title → `Classes="heading"`
- Label above TextBox → `Classes="label"`
- Description text → `Classes="body-sm"`
- TextBox: just remove inline styling, the global `TextBox` style now applies the tokens automatically
- Footer:
  - Cancel → `Classes="secondary"`
  - Clear tag → `Classes="secondary"`
  - Save → `Classes="primary"`
- Replace inline uniform `Padding`/`Margin` with `{StaticResource Pad*}` (Thickness); leave non-uniform margins inline

- [ ] **Step 3: DeleteConfirmDialog refit**

Apply:
- Dialog title → `Classes="heading"`
- Metadata box (Border) → `Classes="card"` + `Padding="{StaticResource PadLg}"`
- Inside the metadata card, eyebrow labels (Captured / Trigger / Size / Tag) → `Classes="label"`
- Body text values → `Classes="body"` or `"numeric"` for size
- Pre-restore alert and tagged-snapshot alert (if currently using inline Border styling) → `Classes="card"` containing a `pill` for the alert type + a `body-sm` description
- Final confirmation paragraph → `Classes="body"`
- Footer:
  - Cancel → `Classes="secondary"`
  - Delete → `Classes="destructive"` (uses the outlined-red style — no fill, just red border + red text)

- [ ] **Step 4: Build**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded.

- [ ] **Step 5: Verify both dialogs**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

For TagEditDialog: click slot → hover a snapshot row → click 🏷 (tag icon). Dialog opens. Expected: heading title, eyebrow label, styled TextBox, three footer buttons. Type a tag, click Save. Verify the tag persists.

For DeleteConfirmDialog: hover the same row → click 🗑 (delete icon). Dialog opens. Expected: heading, metadata card, alert pills if applicable, outlined-red Delete button. Close without deleting. Close the app.

- [ ] **Step 6: Run tests**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass.

- [ ] **Step 7: Commit**

```bash
git add CubicOdysseyVault.UI/Views/TagEditDialog.axaml \
        CubicOdysseyVault.UI/Views/DeleteConfirmDialog.axaml
git commit -m "feat(ui): polish TagEdit + DeleteConfirm dialogs with shared styles"
```

---

## Final verification

After all 15 tasks:

- [ ] **Final build clean**

```bash
dotnet build CubicOdysseyVault.sln
```

Expected: Build succeeded with no warnings.

- [ ] **Final test pass**

```bash
dotnet test CubicOdysseyVault.sln
```

Expected: 144 tests pass (141 existing + 3 new from Task 3).

- [ ] **Smoke run-through of every view**

```bash
dotnet run --project CubicOdysseyVault.Desktop
```

Click through:
1. MainWindow — toolbar (ghost buttons), sidebar (eyebrow + watcher + storage), slot grid (gradient cards, hover lift, selection notch), detail panel (hero shadow, pills, primary/secondary buttons), snapshot history (action fade-in).
2. Settings (toolbar) — eyebrow labels, input focus glow, primary/secondary footer.
3. Onboarding — manually flip `HasCompletedOnboarding` in `settings.json` to `false` and relaunch. Display heading on Step 1, form on Step 2.
4. Inspect a slot → Save inspector → Summary tab (cards, pills, eyebrow labels), Files tab (file list selection, TLV tree).
5. Hover a snapshot history row → Restore icon → RestoreConfirmDialog (hero shadow, pills, primary Restore).
6. Hover a snapshot history row → Tag icon → TagEditDialog (eyebrow label, styled input, primary Save).
7. Hover a snapshot history row → Delete icon → DeleteConfirmDialog (metadata card, destructive Delete).

All views should feel like they belong to the same app. Spacing rhythm consistent. Type hierarchy clear. Buttons consistent.

- [ ] **Mockup compare**

The MainWindow should match `.superpowers/brainstorm/86510-1778158894/content/main-window-polished.html`. Major points:
- Slot card hover lift + selected red border + voxel notch
- Detail panel widened to 400px, hero frame with shadow
- Sidebar Watcher + Storage stat blocks
- Pill style consistent across health/trigger/category

---

## Out of scope (do NOT implement)

These are explicitly NOT part of this plan:
- Light mode / theme toggle
- Layout structural changes (master-detail, list view variant, etc.)
- Map viewer (Phase 2 of new-work — separate plan)
- New ViewModels or new commands beyond the storage-stats derived properties
- New dependencies, package updates
- Localization or accessibility audit
- Per-control template overrides beyond what's listed

---

## Notes for the executor

- **Voxel notch sizing:** `Path Data="M 0 0 L 16 0 L 16 16 Z"` is a small triangle. If it looks wrong (too big, too small, wrong corner), adjust the points or move the Path inside the card's clip region. The point is "small accent shape only on selected".
- **`/template/ ContentPresenter` patterns:** Avalonia 11's Fluent theme uses ContentPresenter inside Button templates. If a button style doesn't take effect, check whether the property needs to go on the button itself or on `/template/ ContentPresenter`. Buttons are the only control where this matters in this plan.
- **`#PART_BorderElement`:** Avalonia 11 names the TextBox's outer border `PART_BorderElement` in its template. If the focus glow isn't visible, the part name may differ — check Avalonia source or use Avalonia DevTools (F12 in debug builds) to inspect the actual visual tree.
- **Emojis in icon buttons (`↺ 🏷 🗑`):** these are Unicode glyphs that the Inter font may or may not render well. If they look broken, swap for SVG `Path` icons or text labels. For the polish pass, accept emoji glyphs unless they're visibly broken.
- **`LetterSpacing` units:** Avalonia's `LetterSpacing` is in pixels (or device-independent units). Value 1.4 ≈ 0.12em at 11px font.
- **Approval gates between phases:** The user has approved the spec. They may still want to pause between Phase A and Phase B (or between B and C) — confirm before proceeding to the next phase. Each phase is independently shippable.
