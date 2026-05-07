using Avalonia.Media;
using Avalonia.Media.Imaging;
using CubicOdysseyVault.Core.SaveContent;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

public sealed class InventoryItemViewModel : ViewModelBase
{
    public InventoryItem Item { get; }
    public IImage? IconBitmap { get; }
    public bool HasIcon => IconBitmap != null;
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

    // Category booleans for pill class bindings in AXAML
    public bool IsCatEquipment    => Item.Category == ItemCategory.Equipment;
    public bool IsCatWeapon       => Item.Category == ItemCategory.Weapon;
    public bool IsCatResource     => Item.Category == ItemCategory.Resource;
    public bool IsCatShipComponent => Item.Category == ItemCategory.ShipComponent;
    public bool IsCatDeployable   => Item.Category == ItemCategory.Deployable;
    public bool IsCatKey          => Item.Category == ItemCategory.Key;
    public bool IsCatShip         => Item.Category == ItemCategory.Ship;
    public bool IsCatOther        => Item.Category == ItemCategory.Other;

    public InventoryItemViewModel(InventoryItem item, SpriteAtlas? atlas = null)
    {
        Item = item;
        CategoryBrush = ResolveBrush(item.Category);
        if (atlas != null && item.Metadata != null && item.Metadata.InvFrame > 0)
            IconBitmap = IconAtlasCache.Slice(atlas, item.Metadata.InvFrame);
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
