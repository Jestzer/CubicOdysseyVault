namespace CubicOdysseyVault.Core.SaveContent;

public sealed record InventoryItem(
    string Identifier,
    int Count,
    ItemMetadata? Metadata)
{
    public string DisplayName => Metadata != null
        ? ItemCatalog.HumanizeIdentifier(Identifier, Metadata.Tier)
        : ItemCatalog.HumanizeIdentifier(Identifier);

    public ItemCategory Category => Metadata != null
        ? ItemCategoryClassifier.Classify(Metadata)
        : ItemCategoryClassifier.ClassifyByIdentifier(Identifier);

    public int Tier => Metadata?.Tier ?? ExtractTrailingTier(Identifier);

    private static int ExtractTrailingTier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return 0;
        var lastDot = identifier.LastIndexOf('.');
        if (lastDot < 0 || lastDot >= identifier.Length - 1) return 0;
        return int.TryParse(identifier[(lastDot + 1)..], out var t) && t <= 99 ? t : 0;
    }
}

public sealed record InventoryContainer(
    string Name,                  // "inventory", "__quickslots", or a synthetic name
    string DisplayName,            // human-friendly: "Backpack", "Quickslots", "Equipped", etc.
    IReadOnlyList<InventoryItem> Items)
{
    public int TotalCount => Items.Sum(i => i.Count);
}
