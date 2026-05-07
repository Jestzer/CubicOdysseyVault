using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Saves;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class IntegrityCheckerTests
{
    [Fact]
    public void HealthySlot_AllFilesNonZero_AndValidScreenshot()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.RootPath;
        Directory.CreateDirectory(slotDir);

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(Path.Combine(slotDir, "93_state.sav"), new byte[] { 5, 6, 7, 8 });
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(imageType: 2));

        var slot = MakeSlot(slotDir);
        var report = IntegrityChecker.InspectSlot(slot);

        Assert.Equal(SlotHealth.Healthy, report.Health);
        Assert.Empty(report.Issues);
        Assert.True(report.HasScreenshot);
        Assert.True(report.ScreenshotHeaderValid);
        Assert.StartsWith("sha256:", report.CombinedHash);
        Assert.Equal(3, report.FileResults.Count);
    }

    [Fact]
    public void NullByteFile_FlagsCorrupted()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.RootPath;

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[100]); // all zeros
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(imageType: 2));

        var slot = MakeSlot(slotDir);
        var report = IntegrityChecker.InspectSlot(slot);

        Assert.Equal(SlotHealth.Corrupted, report.Health);
        Assert.NotEmpty(report.Issues);
        Assert.Contains(report.FileResults, f => f.FileName == "93_meta.sav" && f.AllNull);
    }

    [Fact]
    public void EmptyFile_FlagsSuspicious_NotCorrupted()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.RootPath;

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(slotDir, "93_state.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(imageType: 2));

        var slot = MakeSlot(slotDir);
        var report = IntegrityChecker.InspectSlot(slot);

        Assert.Equal(SlotHealth.Suspicious, report.Health);
        Assert.Contains(report.FileResults, f => f.FileName == "93_meta.sav" && !f.AllNull);
    }

    [Fact]
    public void MissingScreenshot_OnSlot_FlagsSuspicious()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.RootPath;
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2, 3 });

        var slot = MakeSlot(slotDir);
        var report = IntegrityChecker.InspectSlot(slot);

        Assert.Equal(SlotHealth.Suspicious, report.Health);
        Assert.False(report.HasScreenshot);
        Assert.Contains("missing", string.Join(" ", report.Issues), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidTgaHeader_FlagsSuspicious()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.RootPath;
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), new byte[] { 0xFF, 0xFE, 99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

        var slot = MakeSlot(slotDir);
        var report = IntegrityChecker.InspectSlot(slot);

        Assert.Equal(SlotHealth.Suspicious, report.Health);
        Assert.True(report.HasScreenshot);
        Assert.False(report.ScreenshotHeaderValid);
    }

    [Fact]
    public void CombinedHash_IsDeterministic_AcrossEnumerationOrder()
    {
        using var fixture1 = new TempLayoutFixture();
        using var fixture2 = new TempLayoutFixture();

        File.WriteAllBytes(Path.Combine(fixture1.RootPath, "a.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(fixture1.RootPath, "b.sav"), new byte[] { 4, 5, 6 });
        File.WriteAllBytes(Path.Combine(fixture1.RootPath, "screenshot.tga"), MakeTgaHeader(2));

        // Same content, different write order
        File.WriteAllBytes(Path.Combine(fixture2.RootPath, "screenshot.tga"), MakeTgaHeader(2));
        File.WriteAllBytes(Path.Combine(fixture2.RootPath, "b.sav"), new byte[] { 4, 5, 6 });
        File.WriteAllBytes(Path.Combine(fixture2.RootPath, "a.sav"), new byte[] { 1, 2, 3 });

        var report1 = IntegrityChecker.InspectSlot(MakeSlot(fixture1.RootPath));
        var report2 = IntegrityChecker.InspectSlot(MakeSlot(fixture2.RootPath));

        Assert.Equal(report1.CombinedHash, report2.CombinedHash);
    }

    [Fact]
    public void RleTgaHeader_IsAcceptedAsValid()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.RootPath;
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2 });
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(imageType: 10));

        var slot = MakeSlot(slotDir);
        var report = IntegrityChecker.InspectSlot(slot);

        Assert.True(report.ScreenshotHeaderValid);
        Assert.Equal(SlotHealth.Healthy, report.Health);
    }

    [Fact]
    public void AccountInspection_DoesNotRequireScreenshot()
    {
        using var fixture = new TempLayoutFixture();
        var dir = fixture.RootPath;
        File.WriteAllBytes(Path.Combine(dir, "meta.sav"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(dir, "93_blueprints.sav"), new byte[] { 2 });

        var account = MakeAccount(dir);
        var report = IntegrityChecker.InspectAccount(account);

        Assert.Equal(SlotHealth.Healthy, report.Health);
        Assert.False(report.HasScreenshot);
    }

    private static SaveSlot MakeSlot(string dir)
    {
        var files = Directory.EnumerateFiles(dir)
            .Select(p => new SaveSlotFile(Path.GetFileName(p), p, new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)))
            .ToList();
        var hasScreenshot = files.Any(f => f.FileName == "screenshot.tga");
        var src = new SaveSource(SaveSourceKind.Manual, dir, null, true);
        return new SaveSlot("11111111", "0", "0", dir, files, hasScreenshot, src, DateTime.UtcNow, files.Sum(f => f.SizeBytes));
    }

    private static SaveAccount MakeAccount(string dir)
    {
        var files = Directory.EnumerateFiles(dir)
            .Select(p => new SaveAccountFile(Path.GetFileName(p), p, new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)))
            .ToList();
        var src = new SaveSource(SaveSourceKind.Manual, dir, null, true);
        return new SaveAccount("11111111", dir, files, src, DateTime.UtcNow, files.Sum(f => f.SizeBytes));
    }

    // Minimal TGA header: 18 bytes, image type at offset 2.
    private static byte[] MakeTgaHeader(byte imageType)
    {
        var bytes = new byte[18 + 16];
        bytes[2] = imageType;
        // Add some image data so it's not zero-byte (avoids size==0 suspicion)
        for (int i = 18; i < bytes.Length; i++) bytes[i] = (byte)i;
        return bytes;
    }
}
