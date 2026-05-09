using System.Collections.ObjectModel;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.ViewModels;

public sealed class SaveSummaryViewModel : ViewModelBase
{
    public SaveSummary Summary { get; }
    public string CharacterName => string.IsNullOrEmpty(Summary.CharacterName) ? "(unknown)" : Summary.CharacterName;
    public string SavedAtText => Summary.SavedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(unknown)";
    public string SlotHeader => $"Slot {Summary.Slot.SlotName} / acct {Summary.Slot.AccountFolderName} ({Summary.Slot.SteamId32})";

    // Containers are split across two independently-stacked columns instead of a
    // 2-column UniformGrid so a small card (e.g. 2 items) doesn't get padded out
    // to the height of the largest card (which can run 30+ items). Greedy fill in
    // declaration order: each container goes into whichever column has fewer
    // accumulated rows so far. Preserves the canonical inventory order (equipped
    // → quickslots → inventory → ship) within each column.
    public ObservableCollection<InventoryContainerViewModel> LeftContainers { get; } = new();
    public ObservableCollection<InventoryContainerViewModel> RightContainers { get; } = new();
    public ObservableCollection<string> Ships { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    public bool HasShips => Summary.ShipFiles.Count > 0;
    public bool HasInventories => Summary.Inventories.Count > 0;
    public bool HasWarnings => Summary.Warnings.Count > 0;

    public SaveSummaryViewModel(SaveSummary summary)
    {
        Summary = summary;
        int leftWeight = 0, rightWeight = 0;
        foreach (var c in summary.Inventories)
        {
            // Approximate row count: each item is one row, plus a small constant
            // for the title/subtitle header so a 0-item container still occupies
            // some height in the balance.
            var weight = c.Items.Count + 2;
            var vm = new InventoryContainerViewModel(c, summary.IconAtlas);
            if (leftWeight <= rightWeight)
            {
                LeftContainers.Add(vm);
                leftWeight += weight;
            }
            else
            {
                RightContainers.Add(vm);
                rightWeight += weight;
            }
        }
        foreach (var s in summary.ShipFiles)
            Ships.Add(s);
        foreach (var w in summary.Warnings)
            Warnings.Add(w);
    }
}
