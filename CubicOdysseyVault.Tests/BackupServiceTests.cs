using System.Linq;
using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _backupRoot;
    private readonly TempLayoutFixture _saveLayout;

    public BackupServiceTests()
    {
        _backupRoot = Path.Combine(Path.GetTempPath(), $"covtest-backup-{Guid.NewGuid():N}");
        _saveLayout = new TempLayoutFixture();
    }

    public void Dispose()
    {
        _saveLayout.Dispose();
        try { if (Directory.Exists(_backupRoot)) Directory.Delete(_backupRoot, recursive: true); } catch { }
    }

    [Fact]
    public void SnapshotSlot_FirstTime_CopiesFilesAndWritesManifest()
    {
        var slot = MakeRealSlot();
        var svc = new BackupService(_backupRoot);

        var result = svc.SnapshotSlot(slot, SnapshotTrigger.Manual);

        Assert.True(result.Success);
        Assert.False(result.Skipped);
        Assert.NotNull(result.Snapshot);

        var snapDir = Path.Combine(
            _backupRoot, "snapshots", slot.SteamId32, slot.AccountFolderName, slot.SlotName,
            result.Snapshot!.FolderName);
        Assert.True(Directory.Exists(snapDir));

        // All slot files copied
        Assert.Equal(slot.Files.Count, Directory.EnumerateFiles(snapDir).Count());

        // Manifest written
        var manifest = SnapshotIndex.Load(Path.Combine(
            _backupRoot, "snapshots", slot.SteamId32, slot.AccountFolderName, slot.SlotName,
            "manifest.json"));
        Assert.Single(manifest.Snapshots);
        Assert.Equal(result.Snapshot.Id, manifest.Snapshots[0].Id);
    }

    [Fact]
    public void SnapshotSlot_UnchangedSinceLast_SkipsCopy()
    {
        var slot = MakeRealSlot();
        var svc = new BackupService(_backupRoot);

        var first = svc.SnapshotSlot(slot, SnapshotTrigger.Manual);
        Assert.True(first.Success);

        var second = svc.SnapshotSlot(slot, SnapshotTrigger.Manual);
        Assert.True(second.Success);
        Assert.True(second.Skipped);
        Assert.Equal(first.Snapshot!.Id, second.Snapshot!.Id);

        // Only one snapshot folder under the slot
        var slotDir = Path.Combine(_backupRoot, "snapshots", slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        var snapFolders = Directory.EnumerateDirectories(slotDir).ToList();
        Assert.Single(snapFolders);
    }

    [Fact]
    public void SnapshotSlot_ChangedFile_CreatesSecondSnapshot()
    {
        var slot = MakeRealSlot();
        var svc = new BackupService(_backupRoot);

        var first = svc.SnapshotSlot(slot, SnapshotTrigger.Manual);
        Assert.True(first.Success);

        // Mutate one file
        File.WriteAllBytes(slot.Files[0].FullPath, new byte[] { 99, 100, 101 });
        var slot2 = ReReadSlot(slot.SlotFolderPath);

        var second = svc.SnapshotSlot(slot2, SnapshotTrigger.Manual);
        Assert.True(second.Success);
        Assert.False(second.Skipped);
        Assert.NotEqual(first.Snapshot!.Id, second.Snapshot!.Id);

        var manifest = SnapshotIndex.Load(Path.Combine(
            _backupRoot, "snapshots", slot.SteamId32, slot.AccountFolderName, slot.SlotName,
            "manifest.json"));
        Assert.Equal(2, manifest.Snapshots.Count);
    }

    [Fact]
    public void SnapshotSlot_AutoTriggerOnCorruptedSlot_Aborts()
    {
        // Corrupted slot: a file that's all NULLs
        var slotDir = _saveLayout.AddSlot(_saveLayout.AddAccountFolder(_saveLayout.AddSteamId("11111111"), "0"), "0");
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[100]); // all zeros
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(2));

        var slot = ReReadSlot(slotDir);
        var svc = new BackupService(_backupRoot);

        var result = svc.SnapshotSlot(slot, SnapshotTrigger.Auto);
        Assert.False(result.Success);
        Assert.Contains("corrupted", result.Reason ?? "", StringComparison.OrdinalIgnoreCase);

        var slotDirInBackup = Path.Combine(_backupRoot, "snapshots", "11111111", "0", "0");
        Assert.False(Directory.Exists(slotDirInBackup));
    }

    [Fact]
    public void SnapshotSlot_ManualTriggerOnCorruptedSlot_StillAllows()
    {
        // Manual snapshots intentionally permit corrupted slots — user explicitly asked.
        var slotDir = _saveLayout.AddSlot(_saveLayout.AddAccountFolder(_saveLayout.AddSteamId("11111111"), "0"), "0");
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[100]);
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(2));

        var slot = ReReadSlot(slotDir);
        var svc = new BackupService(_backupRoot);

        var result = svc.SnapshotSlot(slot, SnapshotTrigger.Manual);
        Assert.True(result.Success);
        Assert.Equal(SlotHealth.Corrupted, result.Snapshot!.Health);
    }

    [Fact]
    public void SnapshotAccount_RoundTrip()
    {
        var steamIdDir = _saveLayout.AddSteamId("22222222");
        File.WriteAllBytes(Path.Combine(steamIdDir, "meta.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(steamIdDir, "93_blueprints.sav"), new byte[] { 4, 5, 6 });

        var account = ReReadAccount(steamIdDir);
        var svc = new BackupService(_backupRoot);

        var result = svc.SnapshotAccount(account, SnapshotTrigger.Manual);
        Assert.True(result.Success);

        var accountSnapDir = Path.Combine(_backupRoot, "snapshots", "22222222", "_account", result.Snapshot!.FolderName);
        Assert.True(Directory.Exists(accountSnapDir));
        Assert.Equal(2, Directory.EnumerateFiles(accountSnapDir).Count());
    }

    [Fact]
    public void ListSlotSnapshots_AfterMultipleBackups_ReturnsAll()
    {
        var slot = MakeRealSlot();
        var svc = new BackupService(_backupRoot);
        svc.SnapshotSlot(slot, SnapshotTrigger.Manual, "first");

        File.WriteAllBytes(slot.Files[0].FullPath, new byte[] { 99 });
        var slot2 = ReReadSlot(slot.SlotFolderPath);
        svc.SnapshotSlot(slot2, SnapshotTrigger.Manual, "second");

        var snaps = svc.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        Assert.Equal(2, snaps.Count);
        Assert.Contains(snaps, s => s.Tag == "first");
        Assert.Contains(snaps, s => s.Tag == "second");
    }

    private SaveSlot MakeRealSlot()
    {
        var slotDir = _saveLayout.AddSlot(_saveLayout.AddAccountFolder(_saveLayout.AddSteamId("11111111"), "0"), "0");
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(slotDir, "93_state.sav"), new byte[] { 4, 5, 6 });
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

    private static SaveAccount ReReadAccount(string steamIdDir)
    {
        var files = Directory.EnumerateFiles(steamIdDir)
            .Select(p => new SaveAccountFile(Path.GetFileName(p), p, new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)))
            .ToList();
        var src = new SaveSource(SaveSourceKind.Manual, Path.GetDirectoryName(steamIdDir)!, null, true);
        return new SaveAccount("22222222", steamIdDir, files, src, DateTime.UtcNow, files.Sum(f => f.SizeBytes));
    }

    private static byte[] MakeTgaHeader(byte imageType)
    {
        var bytes = new byte[18 + 16];
        bytes[2] = imageType;
        for (int i = 18; i < bytes.Length; i++) bytes[i] = (byte)i;
        return bytes;
    }
}
