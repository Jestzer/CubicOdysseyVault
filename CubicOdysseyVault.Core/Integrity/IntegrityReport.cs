namespace CubicOdysseyVault.Core.Integrity;

public sealed record IntegrityFileResult(
    string FileName,
    string Sha256,
    long SizeBytes,
    bool AllNull);

public sealed record IntegrityReport(
    SlotHealth Health,
    string CombinedHash,
    IReadOnlyList<IntegrityFileResult> FileResults,
    long TotalBytes,
    bool HasScreenshot,
    bool ScreenshotHeaderValid,
    IReadOnlyList<string> Issues);
