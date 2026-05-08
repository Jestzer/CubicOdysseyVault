using System.Linq;
using CubicOdysseyVault.Core.Restore;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class RestoreServiceAccountTests : IDisposable
{
    private readonly string _backupRoot;
    private readonly TempLayoutFixture _liveLayout;

    public RestoreServiceAccountTests()
    {
        _backupRoot = Path.Combine(Path.GetTempPath(), $"covtest-restore-acct-{Guid.NewGuid():N}");
        _liveLayout = new TempLayoutFixture();
    }

    public void Dispose()
    {
        _liveLayout.Dispose();
        try { if (Directory.Exists(_backupRoot)) Directory.Delete(_backupRoot, recursive: true); } catch { }
    }

    [Fact]
    public void RestoreAccount_RoundTrip_RestoresOriginalContent()
    {
        var account = MakeAccount(out var accountDir);
        var backup = new BackupService(_backupRoot);

        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual, "v1");
        Assert.True(v1.Success);

        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 9, 9, 9 });
        var accountV2 = ReReadAccount(accountDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreAccount(accountV2, v1.Snapshot!, backup);

        Assert.True(result.Success);
        Assert.NotNull(result.PreRestoreSnapshot);
        Assert.Equal(SnapshotTrigger.PreRestore, result.PreRestoreSnapshot!.Trigger);
        Assert.NotNull(result.ReplacedFolderPath);
        Assert.True(Directory.Exists(result.ReplacedFolderPath));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(accountDir, "meta.sav")));
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(result.ReplacedFolderPath!, "meta.sav")));
    }

    [Fact]
    public void RestoreAccount_GameRunning_ReturnsBlocked_WithoutTouching()
    {
        var account = MakeAccount(out var accountDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual);
        Assert.True(v1.Success);

        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 9, 9, 9 });
        var accountV2 = ReReadAccount(accountDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => true);
        var result = restore.RestoreAccount(accountV2, v1.Snapshot!, backup);

        Assert.False(result.Success);
        Assert.True(result.BlockedByRunningGame);
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(accountDir, "meta.sav")));
    }

    [Fact]
    public void RestoreAccount_TakesPreRestoreSnapshot_BeforeSwap()
    {
        var account = MakeAccount(out var accountDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual);

        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 7, 7, 7 });
        var accountV2 = ReReadAccount(accountDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreAccount(accountV2, v1.Snapshot!, backup);
        Assert.True(result.Success);

        var snapshots = backup.ListAccountSnapshots(account.SteamId32);
        var pre = snapshots.FirstOrDefault(s => s.Trigger == SnapshotTrigger.PreRestore);
        Assert.NotNull(pre);
        Assert.NotEqual(v1.Snapshot!.CombinedHash, pre!.CombinedHash);
    }

    [Fact]
    public void RestoreAccount_LeavesSlotSubfoldersUntouched()
    {
        var account = MakeAccount(out var accountDir);

        // Add a slot subfolder with content. The account is `<RootPath>/76561.../`
        // so a slot lives at `<accountDir>/0/0/`.
        var slotInner = Path.Combine(accountDir, "0", "0");
        Directory.CreateDirectory(slotInner);
        var slotFilePath = Path.Combine(slotInner, "93_meta.sav");
        File.WriteAllBytes(slotFilePath, new byte[] { 42, 42, 42 });

        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual);
        Assert.True(v1.Success);

        // Mutate account-level data + slot data. We want to confirm slot data
        // is NOT clobbered when we restore the account.
        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 9, 9, 9 });
        File.WriteAllBytes(slotFilePath, new byte[] { 88, 88, 88 });
        var accountV2 = ReReadAccount(accountDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreAccount(accountV2, v1.Snapshot!, backup);
        Assert.True(result.Success);

        // Account-level file restored
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(accountDir, "meta.sav")));
        // Slot file untouched (still the v2 mutation)
        Assert.Equal(new byte[] { 88, 88, 88 }, File.ReadAllBytes(slotFilePath));
        // Slot subfolder still in place
        Assert.True(Directory.Exists(slotInner));
    }

    [Fact]
    public void RestoreAccount_KeepsOnlyOneGenerationOfReplacedFolders()
    {
        var account = MakeAccount(out var accountDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual);

        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 9, 9, 9 });
        var accountV2 = ReReadAccount(accountDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);

        var first = restore.RestoreAccount(accountV2, v1.Snapshot!, backup);
        Assert.True(first.Success);

        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 5, 5, 5 });
        var accountV3 = ReReadAccount(accountDir);

        Thread.Sleep(1100);

        var second = restore.RestoreAccount(accountV3, v1.Snapshot!, backup);
        Assert.True(second.Success);

        var replaced = Directory.EnumerateDirectories(accountDir)
            .Where(d => Path.GetFileName(d).StartsWith("_account-replaced-", StringComparison.Ordinal))
            .ToList();
        Assert.Single(replaced);
        Assert.Equal(second.ReplacedFolderPath, replaced[0]);
    }

    [Fact]
    public void RestoreAccount_MissingSnapshot_FailsCleanly()
    {
        var account = MakeAccount(out var accountDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual);

        var snapshotFolder = Path.Combine(_backupRoot, "snapshots", account.SteamId32, "_account", v1.Snapshot!.FolderName);
        Directory.Delete(snapshotFolder, recursive: true);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreAccount(account, v1.Snapshot!, backup);

        Assert.False(result.Success);
        Assert.Contains("snapshot folder", result.Reason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(accountDir, "meta.sav")));
    }

    [Fact]
    public void RestoreAccount_RemovesExtraLiveFiles_NotInSnapshot()
    {
        var account = MakeAccount(out var accountDir);
        var backup = new BackupService(_backupRoot);
        var v1 = backup.SnapshotAccount(account, SnapshotTrigger.Manual);
        Assert.True(v1.Success);

        // After snapshot, add a new loose file. Restore should remove it
        // (preserved in the replaced folder for undo).
        var extra = Path.Combine(accountDir, "rogue.sav");
        File.WriteAllBytes(extra, new byte[] { 0xFF });
        var accountV2 = ReReadAccount(accountDir);

        var restore = new RestoreService(_backupRoot, isGameRunning: () => false);
        var result = restore.RestoreAccount(accountV2, v1.Snapshot!, backup);
        Assert.True(result.Success);

        Assert.False(File.Exists(extra));
        Assert.True(File.Exists(Path.Combine(result.ReplacedFolderPath!, "rogue.sav")));
    }

    private SaveAccount MakeAccount(out string accountDir)
    {
        accountDir = _liveLayout.AddSteamId("76561198000000001");
        File.WriteAllBytes(Path.Combine(accountDir, "meta.sav"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(accountDir, "93_blueprints.sav"), new byte[] { 4, 5, 6 });
        return ReReadAccount(accountDir);
    }

    private static SaveAccount ReReadAccount(string accountDir)
    {
        var files = Directory.EnumerateFiles(accountDir)
            .Select(p => new SaveAccountFile(Path.GetFileName(p), p, new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)))
            .ToList();
        var src = new SaveSource(SaveSourceKind.Manual, accountDir, OriginatingSteamRoot: null, Exists: true);
        return new SaveAccount(
            "76561198000000001", accountDir, files, src, DateTime.UtcNow, files.Sum(f => f.SizeBytes));
    }
}
