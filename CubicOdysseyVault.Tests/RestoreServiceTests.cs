using System.Linq;
using CubicOdysseyVault.Core.Restore;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class RestoreServiceTests : IDisposable
{
    private readonly string _backupRoot;
    private readonly TempLayoutFixture _liveLayout;

    public RestoreServiceTests()
    {
        _backupRoot = Path.Combine(Path.GetTempPath(), $"covtest-restore-{Guid.NewGuid():N}");
        _liveLayout = new TempLayoutFixture();
    }

    public void Dispose()
    {
        _liveLayout.Dispose();
        try { if (Directory.Exists(_backupRoot)) Directory.Delete(_backupRoot, recursive: true); } catch { }
    }

    [Fact]
    public void RestoreSlot_RoundTrip_RestoresOriginalContent()
    {
        var slot = MakeSlot(out var slotDir);
        var backup = new BackupService(_backupRoot);

        // Snapshot v1 (the one we'll restore to)
        var v1 = backup.SnapshotSlot(slot, SnapshotTrigger.Manual, "v1");
        Assert.True(v1.Success);

        // Mutate the live slot to v2
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 9, 9, 9 });
        var slotV2 = ReReadSlot(slotDir);

        // Restore back to v1
        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreSlot(slotV2, v1.Snapshot!, backup);

        Assert.True(result.Success);
        Assert.NotNull(result.PreRestoreSnapshot);
        Assert.Equal(SnapshotTrigger.PreRestore, result.PreRestoreSnapshot!.Trigger);
        Assert.NotNull(result.ReplacedFolderPath);
        Assert.True(Directory.Exists(result.ReplacedFolderPath));

        // The live slot now holds v1's content
        var live = File.ReadAllBytes(Path.Combine(slotDir, "93_meta.sav"));
        Assert.Equal(new byte[] { 1, 2, 3 }, live);

        // The replaced folder holds v2's content (the pre-restore live state)
        var replaced = File.ReadAllBytes(Path.Combine(result.ReplacedFolderPath!, "93_meta.sav"));
        Assert.Equal(new byte[] { 9, 9, 9 }, replaced);
    }

    [Fact]
    public void RestoreSlot_GameRunning_ReturnsBlocked_WithoutTouching()
    {
        var slot = MakeSlot(out var slotDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotSlot(slot, SnapshotTrigger.Manual);
        Assert.True(v1.Success);

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 9, 9, 9 });
        var slotV2 = ReReadSlot(slotDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => true);
        var result = restore.RestoreSlot(slotV2, v1.Snapshot!, backup);

        Assert.False(result.Success);
        Assert.True(result.BlockedByRunningGame);
        // Live slot unchanged
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(slotDir, "93_meta.sav")));
    }

    [Fact]
    public void RestoreSlot_TakesPreRestoreSnapshot_BeforeSwap()
    {
        var slot = MakeSlot(out var slotDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotSlot(slot, SnapshotTrigger.Manual);

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 7, 7, 7 });
        var slotV2 = ReReadSlot(slotDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreSlot(slotV2, v1.Snapshot!, backup);
        Assert.True(result.Success);

        // The PreRestore snapshot in the manifest should reflect the v2 state (live before restore)
        var snapshots = backup.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        var pre = snapshots.FirstOrDefault(s => s.Trigger == SnapshotTrigger.PreRestore);
        Assert.NotNull(pre);
        // Its CombinedHash should equal the v2 hash, not v1
        Assert.NotEqual(v1.Snapshot!.CombinedHash, pre!.CombinedHash);
    }

    [Fact]
    public void RestoreSlot_KeepsOnlyOneGenerationOfReplacedFolders()
    {
        var slot = MakeSlot(out var slotDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotSlot(slot, SnapshotTrigger.Manual);

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 9, 9, 9 });
        var slotV2 = ReReadSlot(slotDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);

        var first = restore.RestoreSlot(slotV2, v1.Snapshot!, backup);
        Assert.True(first.Success);

        // Mutate again, restore again
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 5, 5, 5 });
        var slotV3 = ReReadSlot(slotDir);

        // Need a slight wait because folder names are second-resolution
        Thread.Sleep(1100);

        var second = restore.RestoreSlot(slotV3, v1.Snapshot!, backup);
        Assert.True(second.Success);

        // Only one .replaced-* folder should remain
        var slotParent = Path.GetDirectoryName(slotDir)!;
        var replaced = Directory.EnumerateDirectories(slotParent)
            .Where(d => Path.GetFileName(d).StartsWith(Path.GetFileName(slotDir) + ".replaced-", StringComparison.Ordinal))
            .ToList();
        Assert.Single(replaced);
        Assert.Equal(second.ReplacedFolderPath, replaced[0]);
    }

    [Fact]
    public void RestoreSlot_MissingSnapshot_FailsCleanly()
    {
        var slot = MakeSlot(out var slotDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotSlot(slot, SnapshotTrigger.Manual);

        // Delete the snapshot folder underneath us
        var snapshotFolder = Path.Combine(_backupRoot, "snapshots", slot.SteamId32, slot.AccountFolderName, slot.SlotName, v1.Snapshot!.FolderName);
        Directory.Delete(snapshotFolder, recursive: true);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreSlot(slot, v1.Snapshot!, backup);

        Assert.False(result.Success);
        Assert.Contains("snapshot folder", result.Reason ?? "", StringComparison.OrdinalIgnoreCase);
        // Live slot unchanged
        Assert.True(Directory.Exists(slotDir));
        Assert.True(File.Exists(Path.Combine(slotDir, "93_meta.sav")));
    }

    private SaveSlot MakeSlot(out string slotDir)
    {
        slotDir = _liveLayout.AddSlot(_liveLayout.AddAccountFolder(_liveLayout.AddSteamId("11111111"), "0"), "0");
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(2));
        return ReReadSlot(slotDir);
    }

    private static SaveSlot ReReadSlot(string slotDir)
    {
        var files = Directory.EnumerateFiles(slotDir)
            .Select(p => new SaveSlotFile(Path.GetFileName(p), p, new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)))
            .ToList();
        var src = new SaveSource(SaveSourceKind.Manual, slotDir, null, true);
        return new SaveSlot(
            "11111111", "0", "0", slotDir,
            files,
            HasScreenshot: files.Any(f => f.FileName == "screenshot.tga"),
            src, DateTime.UtcNow, files.Sum(f => f.SizeBytes));
    }

    private static byte[] MakeTgaHeader(byte imageType)
    {
        var bytes = new byte[18 + 16];
        bytes[2] = imageType;
        for (int i = 18; i < bytes.Length; i++) bytes[i] = (byte)i;
        return bytes;
    }
}
