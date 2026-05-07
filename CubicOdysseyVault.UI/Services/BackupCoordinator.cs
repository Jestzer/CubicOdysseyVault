using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.Services;

public sealed class BackupCoordinator
{
    private BackupService _service;

    public BackupCoordinator(string backupRoot)
    {
        _service = new BackupService(backupRoot);
    }

    public void UpdateBackupRoot(string newRoot)
    {
        _service = new BackupService(newRoot);
    }

    public Task<BackupResult> SnapshotSlotAsync(SaveSlot slot, SnapshotTrigger trigger, string? tag = null) =>
        Task.Run(() => _service.SnapshotSlot(slot, trigger, tag));

    public Task<BackupResult> SnapshotAccountAsync(SaveAccount account, SnapshotTrigger trigger, string? tag = null) =>
        Task.Run(() => _service.SnapshotAccount(account, trigger, tag));

    public IReadOnlyList<Snapshot> ListSlotSnapshots(SaveSlot slot) =>
        _service.ListSlotSnapshots(slot.SteamId32, slot.AccountFolderName, slot.SlotName);

    public IReadOnlyList<Snapshot> ListAccountSnapshots(SaveAccount account) =>
        _service.ListAccountSnapshots(account.SteamId32);
}
