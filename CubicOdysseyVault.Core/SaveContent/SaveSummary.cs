using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.Core.SaveContent;

// FileName + FullPath for each ship_*.vx in a slot. Path is needed so the
// UI can decode the voxel grid to a thumbnail; the bare filename is kept
// as an identifier for callers that don't need to read the file.
public sealed record ShipFile(string FileName, string FullPath);

public sealed record SaveSummary(
    SaveSlot Slot,
    string? CharacterName,
    DateTime? SavedAtUtc,
    IReadOnlyList<InventoryContainer> Inventories,
    IReadOnlyList<ShipFile> ShipFiles,
    IReadOnlyList<string> Warnings,
    SpriteAtlas? IconAtlas);
