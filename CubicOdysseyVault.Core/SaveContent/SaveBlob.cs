namespace CubicOdysseyVault.Core.SaveContent;

public sealed record SaveBlob(
    byte[] RawBytes,
    byte[]? DecompressedBytes,
    string? ErrorMessage);
