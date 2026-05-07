using System.Linq;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class SnapshotTagDeleteTests : IDisposable
{
    private readonly string _backupRoot;
    private readonly TempLayoutFixture _layout;

    public SnapshotTagDeleteTests()
    {
        _backupRoot = Path.Combine(Path.GetTempPath(), $"covtest-tagdel-{Guid.NewGuid():N}");
        _layout = new TempLayoutFixture();
    }

    public void Dispose()
    {
        _layout.Dispose();
        try { if (Directory.Exists(_backupRoot)) Directory.Delete(_backupRoot, recursive: true); } catch { }
    }

    [Fact]
    public void UpdateSlotSnapshotTag_RoundTrips()
    {
        var (svc, slot, snapshot) = ArrangeSlotSnapshot();

        var ok = svc.UpdateSlotSnapshotTag(slot.SteamId32, slot.AccountFolderName, slot.SlotName, snapshot.Id, "milestone-1");
        Assert.True(ok);

        var listed = svc.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        var found = listed.Single(s => s.Id == snapshot.Id);
        Assert.Equal("milestone-1", found.Tag);
    }

    [Fact]
    public void UpdateSlotSnapshotTag_EmptyValue_ClearsTag()
    {
        var (svc, slot, snapshot) = ArrangeSlotSnapshot();
        svc.UpdateSlotSnapshotTag(slot.SteamId32, slot.AccountFolderName, slot.SlotName, snapshot.Id, "first");
        var afterSet = svc.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName).Single();
        Assert.Equal("first", afterSet.Tag);

        svc.UpdateSlotSnapshotTag(slot.SteamId32, slot.AccountFolderName, slot.SlotName, snapshot.Id, "   ");
        var afterClear = svc.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName).Single();
        Assert.Null(afterClear.Tag);
    }

    [Fact]
    public void UpdateSlotSnapshotTag_UnknownId_ReturnsFalse()
    {
        var (svc, slot, _) = ArrangeSlotSnapshot();
        var ok = svc.UpdateSlotSnapshotTag(slot.SteamId32, slot.AccountFolderName, slot.SlotName, "not-a-real-id", "tag");
        Assert.False(ok);
    }

    [Fact]
    public void DeleteSlotSnapshot_RemovesFolderAndManifestEntry()
    {
        var (svc, slot, snapshot) = ArrangeSlotSnapshot();
        var snapshotFolder = Path.Combine(
            _backupRoot, "snapshots", slot.SteamId32, slot.AccountFolderName, slot.SlotName,
            snapshot.FolderName);
        Assert.True(Directory.Exists(snapshotFolder));

        var ok = svc.DeleteSlotSnapshot(slot.SteamId32, slot.AccountFolderName, slot.SlotName, snapshot.Id);
        Assert.True(ok);

        Assert.False(Directory.Exists(snapshotFolder));
        Assert.Empty(svc.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName));
    }

    [Fact]
    public void DeleteSlotSnapshot_UnknownId_ReturnsFalse()
    {
        var (svc, slot, _) = ArrangeSlotSnapshot();
        var ok = svc.DeleteSlotSnapshot(slot.SteamId32, slot.AccountFolderName, slot.SlotName, "missing-id");
        Assert.False(ok);
    }

    [Fact]
    public void DeleteAccountSnapshot_RemovesFolderAndManifestEntry()
    {
        var svc = new BackupService(_backupRoot);
        var dir = _layout.AddSteamId("99999999");
        File.WriteAllBytes(Path.Combine(dir, "meta.sav"), new byte[] { 1, 2, 3 });
        var account = new SaveAccount(
            "99999999", dir,
            new[] { new SaveAccountFile("meta.sav", Path.Combine(dir, "meta.sav"), 3, DateTime.UtcNow) },
            new SaveSource(SaveSourceKind.Manual, dir, null, true),
            DateTime.UtcNow, 3);

        var snap = svc.SnapshotAccount(account, SnapshotTrigger.Manual, "before");
        Assert.True(snap.Success);
        var folder = Path.Combine(_backupRoot, "snapshots", "99999999", "_account", snap.Snapshot!.FolderName);
        Assert.True(Directory.Exists(folder));

        var ok = svc.DeleteAccountSnapshot("99999999", snap.Snapshot.Id);
        Assert.True(ok);
        Assert.False(Directory.Exists(folder));
        Assert.Empty(svc.ListAccountSnapshots("99999999"));
    }

    [Fact]
    public void UpdateTag_PromotesAutoSnapshotToAlwaysKept_NextRetentionWontPrune()
    {
        // Two auto snapshots same hour bucket: latest kept, older pruned. Tag the older
        // one — retention should now keep both because tagged snapshots are always kept.
        var svc = new BackupService(_backupRoot);
        var slotDir = _layout.AddSlot(_layout.AddAccountFolder(_layout.AddSteamId("11111111"), "0"), "0");
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1 });
        var slot = ReReadSlot(slotDir);

        var first = svc.SnapshotSlot(slot, SnapshotTrigger.Auto);
        Assert.True(first.Success);

        // Tag the first snapshot before the second one runs retention against it
        var ok = svc.UpdateSlotSnapshotTag(slot.SteamId32, slot.AccountFolderName, slot.SlotName, first.Snapshot!.Id, "keep-me");
        Assert.True(ok);

        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 2 });
        var slot2 = ReReadSlot(slotDir);
        var second = svc.SnapshotSlot(slot2, SnapshotTrigger.Auto);
        Assert.True(second.Success);

        var snaps = svc.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        Assert.Equal(2, snaps.Count);
        Assert.Contains(snaps, s => s.Tag == "keep-me");
    }

    private (BackupService, SaveSlot, Snapshot) ArrangeSlotSnapshot()
    {
        var svc = new BackupService(_backupRoot);
        var slotDir = _layout.AddSlot(_layout.AddAccountFolder(_layout.AddSteamId("11111111"), "0"), "0");
        File.WriteAllBytes(Path.Combine(slotDir, "93_meta.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(slotDir, "screenshot.tga"), MakeTgaHeader(2));
        var slot = ReReadSlot(slotDir);
        var snap = svc.SnapshotSlot(slot, SnapshotTrigger.Manual);
        Assert.True(snap.Success);
        return (svc, slot, snap.Snapshot!);
    }

    private static SaveSlot ReReadSlot(string slotDir)
    {
        var files = Directory.EnumerateFiles(slotDir)
            .Select(p => new SaveSlotFile(Path.GetFileName(p), p, new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)))
            .ToList();
        var src = new SaveSource(SaveSourceKind.Manual, slotDir, null, true);
        return new SaveSlot(
            "11111111", "0", "0", slotDir, files,
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
