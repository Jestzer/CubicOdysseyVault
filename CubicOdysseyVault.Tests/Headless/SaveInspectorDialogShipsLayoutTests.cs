using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CubicOdysseyVault.Core.SaveContent;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.UI.ViewModels;
using CubicOdysseyVault.UI.Views;
using Xunit;

namespace CubicOdysseyVault.Tests.Headless;

// Regression tests for the SHIPS card visibility bug:
// previously, at max scroll the last ship chip ended up clipped behind the
// fixed close-bar at the bottom of the dialog because the outer
// ScrollViewer's measure of the inner ItemsControl/WrapPanel chain
// under-reported the SHIPS card's true rendered height. These tests open
// the real SaveInspectorDialog under a headless session, pump the layout,
// scroll the outer ScrollViewer to the bottom, and assert that no ship
// chip extends beyond the close-bar's top.
public class SaveInspectorDialogShipsLayoutTests
{
    [AvaloniaFact]
    public void ShipsCard_FewShips_FullyVisibleAtMaxScroll()
        => RunShipsVisibilityCase(shipCount: 6, inventoryItemCount: 30);

    [AvaloniaFact]
    public void ShipsCard_ManyShips_FullyVisibleAtMaxScroll()
        => RunShipsVisibilityCase(shipCount: 24, inventoryItemCount: 30);

    [AvaloniaFact]
    public void ShipsCard_OneShip_FullyVisibleAtMaxScroll()
        => RunShipsVisibilityCase(shipCount: 1, inventoryItemCount: 5);

    [AvaloniaFact]
    public void ShipsCard_AtMinWindowSize_FullyVisibleAtMaxScroll()
        => RunShipsVisibilityCase(shipCount: 12, inventoryItemCount: 20, width: 1000, height: 640);

    private static void RunShipsVisibilityCase(int shipCount, int inventoryItemCount, double width = 1200, double height = 780)
    {
        var dialog = BuildDialog(shipCount, inventoryItemCount, width, height);
        try
        {
            dialog.Show();
            ForceLayout(dialog);

            var scrollViewer = FindSummaryScrollViewer(dialog);
            scrollViewer.Offset = new Vector(0, scrollViewer.Extent.Height);
            ForceLayout(dialog);

            var closeBar = dialog.FindControl<Border>("CloseBar")
                ?? throw new InvalidOperationException("CloseBar not found");
            var shipsCard = dialog.FindControl<Border>("ShipsCard")
                ?? throw new InvalidOperationException("ShipsCard not found");

            var closeBarTopInDialog = closeBar.TranslatePoint(new Point(0, 0), dialog)!.Value.Y;

            string Diagnostics() =>
                $"window=({dialog.Bounds.Width}x{dialog.Bounds.Height}) " +
                $"closeBarTop={closeBarTopInDialog:0.0} " +
                $"sv.Extent={scrollViewer.Extent} sv.Viewport={scrollViewer.Viewport} " +
                $"sv.Offset={scrollViewer.Offset} " +
                $"shipsCard.Bounds={shipsCard.Bounds}";

            // The card itself must be entirely above the close-bar.
            var shipsCardBottomInDialog =
                shipsCard.TranslatePoint(new Point(0, shipsCard.Bounds.Height), dialog)!.Value.Y;
            Assert.True(
                shipsCardBottomInDialog <= closeBarTopInDialog + 0.5,
                $"SHIPS card bottom {shipsCardBottomInDialog:0.0} extends below close-bar top {closeBarTopInDialog:0.0} (overlap {shipsCardBottomInDialog - closeBarTopInDialog:0.0}px). {Diagnostics()}");

            // And every ship chip — find each by its bound TextBlock — must
            // also be fully above the close-bar (not just rendered into the
            // card's bounds — they must be visually onscreen).
            var chipBottoms = FindChipBottomsInDialog(shipsCard, dialog).ToList();
            Assert.Equal(shipCount, chipBottoms.Count);
            foreach (var (text, bottomY) in chipBottoms)
            {
                Assert.True(
                    bottomY <= closeBarTopInDialog + 0.5,
                    $"Chip '{text}' bottom {bottomY:0.0} extends below close-bar top {closeBarTopInDialog:0.0} (overlap {bottomY - closeBarTopInDialog:0.0}px). {Diagnostics()}");
            }
        }
        finally
        {
            dialog.Close();
        }
    }

    private static SaveInspectorDialog BuildDialog(int shipCount, int inventoryItemCount, double width, double height)
    {
        var slot = new SaveSlot(
            SteamId32: "0",
            AccountFolderName: "0",
            SlotName: "0",
            SlotFolderPath: "/tmp/test-slot",
            Files: Array.Empty<SaveSlotFile>(),
            HasScreenshot: false,
            Source: new SaveSource(SaveSourceKind.Manual, "/tmp", null, true),
            LastWriteUtc: new DateTime(2026, 5, 8, 23, 26, 19, DateTimeKind.Utc),
            TotalBytes: 0);

        var inventory = new InventoryContainer(
            Name: "inventory",
            DisplayName: "Backpack",
            Items: Enumerable.Range(0, inventoryItemCount)
                .Select(i => new InventoryItem(
                    Identifier: $"item.test.{i}",
                    Count: 1,
                    Metadata: null))
                .ToList());

        var ships = Enumerable.Range(1, shipCount)
            .Select(i => $"ship_{i}.vx")
            .ToList();

        var summary = new SaveSummary(
            Slot: slot,
            CharacterName: "Test",
            SavedAtUtc: slot.LastWriteUtc,
            Inventories: new[] { inventory },
            ShipFiles: ships,
            Warnings: Array.Empty<string>(),
            IconAtlas: null);

        var vm = new SaveInspectorViewModel(slot, summary);
        return new SaveInspectorDialog
        {
            DataContext = vm,
            Width = width,
            Height = height,
        };
    }

    private static void ForceLayout(Window window)
    {
        // Drain the dispatcher and run a measure/arrange pass — under the
        // headless platform the visual tree isn't laid out until we pump
        // the dispatcher and explicitly size the window.
        Dispatcher.UIThread.RunJobs();
        var size = new Size(window.Width, window.Height);
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static ScrollViewer FindSummaryScrollViewer(Window dialog)
        => dialog.FindControl<ScrollViewer>("SummaryScrollViewer")
            ?? throw new InvalidOperationException("SummaryScrollViewer not found");

    private static IEnumerable<(string text, double bottomY)> FindChipBottomsInDialog(Border shipsCard, Window dialog)
    {
        // Each ship chip is a TextBlock inside a Border inside the SHIPS card.
        // Walk the visual descendants and pick out the ones whose Text matches
        // our ship_*.vx pattern.
        foreach (var tb in shipsCard.GetVisualDescendants().OfType<TextBlock>())
        {
            if (tb.Text is null || !tb.Text.StartsWith("ship_") || !tb.Text.EndsWith(".vx"))
                continue;
            var chipBorder = tb.FindAncestorOfType<Border>();
            if (chipBorder is null)
                continue;
            var bottomInDialog =
                chipBorder.TranslatePoint(new Point(0, chipBorder.Bounds.Height), dialog)!.Value.Y;
            yield return (tb.Text, bottomInDialog);
        }
    }
}

internal static class VisualTreeExtensions
{
    public static T? FindAncestorOfType<T>(this Visual visual) where T : Visual
    {
        var current = visual.GetVisualParent();
        while (current is not null)
        {
            if (current is T match) return match;
            current = current.GetVisualParent();
        }
        return null;
    }
}
