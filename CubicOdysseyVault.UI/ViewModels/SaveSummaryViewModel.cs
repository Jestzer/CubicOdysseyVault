using System.Collections.ObjectModel;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.ViewModels;

public sealed class SaveSummaryViewModel : ViewModelBase
{
    public SaveSummary Summary { get; }
    public string CharacterName => string.IsNullOrEmpty(Summary.CharacterName) ? "(unknown)" : Summary.CharacterName;
    public string SavedAtText => Summary.SavedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(unknown)";
    public string SlotHeader => $"Slot {Summary.Slot.SlotName} / acct {Summary.Slot.AccountFolderName} ({Summary.Slot.SteamId32})";

    public ObservableCollection<InventoryContainerViewModel> Containers { get; } = new();
    public ObservableCollection<string> Ships { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    public bool HasShips => Summary.ShipFiles.Count > 0;
    public bool HasInventories => Summary.Inventories.Count > 0;
    public bool HasWarnings => Summary.Warnings.Count > 0;

    public SaveSummaryViewModel(SaveSummary summary)
    {
        Summary = summary;
        foreach (var c in summary.Inventories)
            Containers.Add(new InventoryContainerViewModel(c));
        foreach (var s in summary.ShipFiles)
            Ships.Add(s);
        foreach (var w in summary.Warnings)
            Warnings.Add(w);
    }
}
