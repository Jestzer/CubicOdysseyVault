namespace CubicOdysseyVault.Core.Saves;

public sealed record SaveAccountFile(
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime LastWriteUtc);

public sealed record SaveAccount(
    string SteamId32,
    string AccountFolderPath,
    IReadOnlyList<SaveAccountFile> AccountFiles,
    SaveSource Source,
    DateTime LastWriteUtc,
    long TotalBytes);
