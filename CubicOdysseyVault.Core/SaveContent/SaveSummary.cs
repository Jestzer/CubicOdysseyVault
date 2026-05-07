using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.Core.SaveContent;

public sealed record SaveSummary(
    SaveSlot Slot,
    string? CharacterName,
    DateTime? SavedAtUtc,
    IReadOnlyList<InventoryContainer> Inventories,
    IReadOnlyList<string> ShipFiles,
    IReadOnlyList<string> Warnings,
    SpriteAtlas? IconAtlas);
