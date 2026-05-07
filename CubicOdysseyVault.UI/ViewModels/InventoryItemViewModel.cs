using Avalonia.Media;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.ViewModels;

public sealed class InventoryItemViewModel : ViewModelBase
{
    public InventoryItem Item { get; }
    public string DisplayName => Item.DisplayName;
    public string Identifier => Item.Identifier;
    public int Count => Item.Count;
    public string CountText => $"×{Item.Count}";
    public string CategoryAbbreviation => Item.Category switch
    {
        ItemCategory.Equipment => "GEAR",
        ItemCategory.Weapon => "WEP",
        ItemCategory.Resource => "RES",
        ItemCategory.ShipComponent => "SHIP",
        ItemCategory.Deployable => "DPL",
        ItemCategory.Key => "KEY",
        ItemCategory.Ship => "SHIP",
        _ => "MISC",
    };
    public IBrush CategoryBrush { get; }
    public string TierText => Item.Tier > 0 ? $"T{Item.Tier}" : "";
    public bool HasTier => Item.Tier > 0;
    public string TypeRaw => Item.Metadata?.TypeRaw ?? "";

    public InventoryItemViewModel(InventoryItem item)
    {
        Item = item;
        CategoryBrush = ResolveBrush(item.Category);
    }

    private static IBrush ResolveBrush(ItemCategory cat)
    {
        var key = cat switch
        {
            ItemCategory.Equipment => "CategoryEquipmentBrush",
            ItemCategory.Weapon => "CategoryWeaponBrush",
            ItemCategory.Resource => "CategoryResourceBrush",
            ItemCategory.ShipComponent => "CategoryShipComponentBrush",
            ItemCategory.Deployable => "CategoryDeployableBrush",
            ItemCategory.Key => "CategoryKeyBrush",
            ItemCategory.Ship => "CategoryShipBrush",
            _ => "CategoryOtherBrush",
        };
        var app = Avalonia.Application.Current;
        if (app != null && app.Resources.TryGetResource(key, app.ActualThemeVariant, out var resource)
            && resource is IBrush ib)
        {
            return ib;
        }
        return Brushes.Gray;
    }
}
