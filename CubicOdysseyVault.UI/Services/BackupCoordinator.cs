using CubicOdysseyVault.Core.Restore;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.Services;

public sealed class BackupCoordinator
{
    private string _backupRoot;
    private BackupService _service;
    private RestoreService _restoreService;

    public BackupCoordinator(string backupRoot, RetentionPolicy.Settings? retention = null)
    {
        _backupRoot = backupRoot;
        _service = new BackupService(_backupRoot, retention);
        _restoreService = new RestoreService(_backupRoot);
    }

    public void Update(string backupRoot, RetentionPolicy.Settings? retention = null)
    {
        _backupRoot = backupRoot;
        _service = new BackupService(_backupRoot, retention);
        _restoreService = new RestoreService(_backupRoot);
    }

    public Task<BackupResult> SnapshotSlotAsync(SaveSlot slot, SnapshotTrigger trigger, string? tag = null) =>
        Task.Run(() => _service.SnapshotSlot(slot, trigger, tag));

    public Task<BackupResult> SnapshotAccountAsync(SaveAccount account, SnapshotTrigger trigger, string? tag = null) =>
        Task.Run(() => _service.SnapshotAccount(account, trigger, tag));

    public IReadOnlyList<Snapshot> ListSlotSnapshots(SaveSlot slot) =>
        _service.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName);

    public IReadOnlyList<Snapshot> ListAccountSnapshots(SaveAccount account) =>
        _service.ListAccountSnapshots(account.SteamId32);

    public Task<RestoreResult> RestoreSlotAsync(SaveSlot slot, Snapshot snapshot) =>
        Task.Run(() => _restoreService.RestoreSlot(slot, snapshot, _service));

    public Task<RestoreResult> RestoreAccountAsync(SaveAccount account, Snapshot snapshot) =>
        Task.Run(() => _restoreService.RestoreAccount(account, snapshot, _service));

    public string GetSlotSnapshotFolder(SaveSlot slot, Snapshot snapshot) =>
        Path.Combine(
            SnapshotStore.GetSlotSnapshotsRoot(_backupRoot, slot.SteamId32, slot.AccountFolderName, slot.SlotName),
            snapshot.FolderName);

    public string GetAccountSnapshotFolder(SaveAccount account, Snapshot snapshot) =>
        Path.Combine(
            SnapshotStore.GetAccountSnapshotsRoot(_backupRoot, account.SteamId32),
            snapshot.FolderName);

    public BackupStoreLayout EnumerateBackupStore() =>
        BackupStoreEnumerator.Enumerate(_backupRoot);

    public IReadOnlyList<Snapshot> ListAccountSnapshots(string steamId32) =>
        _service.ListAccountSnapshots(steamId32);

    public IReadOnlyList<Snapshot> ListSlotSnapshots(string steamId32, string accountFolderName, string slotName) =>
        _service.ListSlotSnapshots(steamId32, accountFolderName, slotName);

    public Task<bool> UpdateSlotSnapshotTagAsync(SaveSlot slot, string snapshotId, string? newTag) =>
        Task.Run(() => _service.UpdateSlotSnapshotTag(slot.SteamId32, slot.AccountFolderName, slot.SlotName, snapshotId, newTag));

    public Task<bool> DeleteSlotSnapshotAsync(SaveSlot slot, string snapshotId) =>
        Task.Run(() => _service.DeleteSlotSnapshot(slot.SteamId32, slot.AccountFolderName, slot.SlotName, snapshotId));

    public Task<bool> UpdateAccountSnapshotTagAsync(SaveAccount account, string snapshotId, string? newTag) =>
        Task.Run(() => _service.UpdateAccountSnapshotTag(account.SteamId32, snapshotId, newTag));

    public Task<bool> DeleteAccountSnapshotAsync(SaveAccount account, string snapshotId) =>
        Task.Run(() => _service.DeleteAccountSnapshot(account.SteamId32, snapshotId));
}
