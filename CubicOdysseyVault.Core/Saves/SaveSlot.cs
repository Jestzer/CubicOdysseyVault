namespace CubicOdysseyVault.Core.Saves;

public sealed record SaveSlotFile(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime LastWriteUtc);

public sealed record SaveSlot(
    string SteamId32,
    string AccountFolderName,
    string SlotName,
    string SlotFolderPath,
    IReadOnlyList<SaveSlotFile> Files,
    bool HasScreenshot,
    SaveSource Source,
    DateTime LastWriteUtc,
    long TotalBytes);
