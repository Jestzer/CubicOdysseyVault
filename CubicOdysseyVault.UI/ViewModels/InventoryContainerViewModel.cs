using System.Collections.ObjectModel;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.ViewModels;

public sealed class InventoryContainerViewModel : ViewModelBase
{
    public InventoryContainer Container { get; }
    public string DisplayName => Container.DisplayName;
    public int EntryCount => Container.Items.Count;
    public string Subtitle => Container.Items.Count == 1
        ? "1 item"
        : $"{Container.Items.Count} items";

    public ObservableCollection<InventoryItemViewModel> Items { get; } = new();

    public InventoryContainerViewModel(InventoryContainer container, SpriteAtlas? atlas = null)
    {
        Container = container;
        foreach (var i in container.Items)
            Items.Add(new InventoryItemViewModel(i, atlas));
    }
}
