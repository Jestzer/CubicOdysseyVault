namespace CubicOdysseyVault.Core.SaveContent;

public sealed record ItemMetadata(
    string Identifier,
    string TitleString,         // Localization key, e.g. "STR_SUIT_2" — falls back to humanized identifier when null/empty
    string TypeRaw,              // Game's type enum: GEAR, RESOURCE, SHIP_COMPONENT_*, etc.
    int Tier,
    int InvFrame,
    int StackSize,
    int RecyclePrice,
    int BasePrice);

public enum ItemCategory
{
    Equipment,        // GEAR, DRONE_GEAR
    Weapon,           // identified by wep.* identifier prefix
    Resource,         // RESOURCE, RAW_ORE, PROCESSED_ORE, CONSUMABLE, FRUIT, SEED, SAPLING, EGG, AMMO
    ShipComponent,    // SHIP_COMPONENT_*
    Deployable,       // DEPLOYABLE, HABITAT, MOD, PART
    Key,              // KEY
    Ship,             // SHIP
    Other,            // unknown / catch-all
}

public static class ItemCategoryClassifier
{
    public static ItemCategory Classify(ItemMetadata meta) =>
        ClassifyFromIdentifierPrefix(meta.Identifier) ?? ClassifyFromType(meta.TypeRaw);

    public static ItemCategory ClassifyByIdentifier(string identifier) =>
        ClassifyFromIdentifierPrefix(identifier) ?? ItemCategory.Other;

    private static ItemCategory? ClassifyFromIdentifierPrefix(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;
        if (identifier.StartsWith("wep.", StringComparison.OrdinalIgnoreCase)) return ItemCategory.Weapon;
        if (identifier.StartsWith("comp.", StringComparison.OrdinalIgnoreCase)) return ItemCategory.ShipComponent;
        if (identifier.StartsWith("dpl.", StringComparison.OrdinalIgnoreCase)) return ItemCategory.Deployable;
        if (identifier.StartsWith("cloth.", StringComparison.OrdinalIgnoreCase)) return ItemCategory.Equipment;
        if (identifier.StartsWith("res.", StringComparison.OrdinalIgnoreCase)) return ItemCategory.Resource;
        return null;
    }

    private static ItemCategory ClassifyFromType(string typeRaw) => typeRaw switch
    {
        "GEAR" or "DRONE_GEAR" => ItemCategory.Equipment,
        "RESOURCE" or "RAW_ORE" or "PROCESSED_ORE" or "CONSUMABLE" or "FRUIT" or "SEED" or "SAPLING" or "EGG" or "AMMO" or "DARK_RESOURCE" or "CREATURE_RESOURCE" => ItemCategory.Resource,
        var t when t != null && t.StartsWith("SHIP_COMPONENT", StringComparison.Ordinal) => ItemCategory.ShipComponent,
        "DEPLOYABLE" or "HABITAT" or "MOD" or "PART" => ItemCategory.Deployable,
        "KEY" => ItemCategory.Key,
        "SHIP" => ItemCategory.Ship,
        _ => ItemCategory.Other,
    };
}
