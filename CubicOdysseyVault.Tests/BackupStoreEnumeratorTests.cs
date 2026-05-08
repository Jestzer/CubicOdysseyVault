using System.Linq;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class BackupStoreEnumeratorTests : IDisposable
{
    private readonly string _backupRoot;

    public BackupStoreEnumeratorTests()
    {
        _backupRoot = Path.Combine(Path.GetTempPath(), $"covtest-storeenum-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_backupRoot)) Directory.Delete(_backupRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Enumerate_NonExistentRoot_ReturnsEmpty()
    {
        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);
        Assert.Empty(layout.Accounts);
        Assert.Empty(layout.Slots);
    }

    [Fact]
    public void Enumerate_AccountOnlySteamId_ReturnsOneAccount()
    {
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "75412417", "_account"));

        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);

        var account = Assert.Single(layout.Accounts);
        Assert.Equal("75412417", account.SteamId32);
        Assert.Empty(layout.Slots);
    }

    [Fact]
    public void Enumerate_SlotOnly_ReturnsOneSlot()
    {
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "76561198", "0", "3"));

        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);

        Assert.Empty(layout.Accounts);
        var slot = Assert.Single(layout.Slots);
        Assert.Equal("76561198", slot.SteamId32);
        Assert.Equal("0", slot.AccountFolderName);
        Assert.Equal("3", slot.SlotName);
    }

    [Fact]
    public void Enumerate_AccountAndSlots_AcrossSteamIds()
    {
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "11111111", "_account"));
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "11111111", "0", "0"));
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "11111111", "0", "1"));
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "22222222", "0", "0"));

        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);

        Assert.Single(layout.Accounts);
        Assert.Equal(3, layout.Slots.Count);
        Assert.Contains(layout.Slots, s => s.SteamId32 == "11111111" && s.SlotName == "0");
        Assert.Contains(layout.Slots, s => s.SteamId32 == "11111111" && s.SlotName == "1");
        Assert.Contains(layout.Slots, s => s.SteamId32 == "22222222" && s.SlotName == "0");
    }

    [Fact]
    public void Enumerate_SkipsNonDigitSteamIds()
    {
        // _account-restore-tmp etc. could end up here if a restore is interrupted —
        // and just generally, only digit-only directories are valid SteamIDs.
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "_account-restore-tmp", "_account"));
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "junk", "_account"));
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "76561198", "_account"));

        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);

        var account = Assert.Single(layout.Accounts);
        Assert.Equal("76561198", account.SteamId32);
    }

    [Fact]
    public void Enumerate_SkipsEntriesWithoutManifest()
    {
        // A folder under a SteamID without a manifest.json is treated as malformed
        // and skipped. Prevents fabricating users from stray directory debris.
        var slotDirNoManifest = Path.Combine(_backupRoot, "snapshots", "11111111", "0", "0");
        Directory.CreateDirectory(slotDirNoManifest);
        // A sibling slot WITH a manifest — only this one should appear.
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "11111111", "0", "1"));

        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);

        var slot = Assert.Single(layout.Slots);
        Assert.Equal("1", slot.SlotName);
    }

    [Fact]
    public void Enumerate_AccountFolderNamed_account_IsNotMistakenForSlotPath()
    {
        // The _account sentinel sits at the same depth as a real account folder,
        // but it should never be treated as one (it has no slot subfolders, just
        // its own manifest + snapshot folders).
        WriteManifest(Path.Combine(_backupRoot, "snapshots", "11111111", "_account"));

        // Drop a stray subfolder under _account to make sure we don't wander into it.
        Directory.CreateDirectory(Path.Combine(_backupRoot, "snapshots", "11111111", "_account", "stray-snapshot-folder"));

        var layout = BackupStoreEnumerator.Enumerate(_backupRoot);

        Assert.Single(layout.Accounts);
        Assert.Empty(layout.Slots);
    }

    private static void WriteManifest(string snapshotsRoot)
    {
        Directory.CreateDirectory(snapshotsRoot);
        File.WriteAllText(Path.Combine(snapshotsRoot, "manifest.json"), "{\"SchemaVersion\":1,\"Snapshots\":[]}");
    }
}
