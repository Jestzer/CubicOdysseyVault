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

    public string GetSlotSnapshotFolder(SaveSlot slot, Snapshot snapshot) =>
        Path.Combine(
            SnapshotStore.GetSlotSnapshotsRoot(_backupRoot, slot.SteamId32, slot.AccountFolderName, slot.SlotName),
            snapshot.FolderName);
}
