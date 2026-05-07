using System.Linq;
using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.Core.Snapshots;

public sealed class BackupService
{
    private readonly string _backupRoot;
    private readonly RetentionPolicy.Settings _retention;

    public BackupService(string backupRoot, RetentionPolicy.Settings? retention = null)
    {
        _backupRoot = backupRoot;
        _retention = retention ?? RetentionPolicy.Settings.Default;
    }

    public BackupResult SnapshotSlot(SaveSlot slot, SnapshotTrigger trigger, string? tag = null)
    {
        var report = IntegrityChecker.InspectSlot(slot);

        if (trigger == SnapshotTrigger.Auto && report.Health == SlotHealth.Corrupted)
            return new BackupResult { Success = false, Reason = "Slot is corrupted; auto-snapshot abandoned." };

        var snapshotsRoot = SnapshotStore.GetSlotSnapshotsRoot(
            _backupRoot, slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        var manifestPath = SnapshotStore.GetManifestPath(snapshotsRoot);
        var manifest = SnapshotIndex.Load(manifestPath);

        var lastSnap = LatestSnapshot(manifest);
        if (lastSnap != null && lastSnap.CombinedHash == report.CombinedHash)
        {
            return new BackupResult
            {
                Success = true, Skipped = true, Snapshot = lastSnap,
                Reason = "No changes since last snapshot.",
            };
        }

        var capturedAt = DateTime.UtcNow;
        var folderName = BuildFolderName(capturedAt, report.CombinedHash);
        var snapshotFolder = Path.Combine(snapshotsRoot, folderName);

        SnapshotStore.CopyFilesAtomically(
            slot.Files.Select(f => (f.FullPath, f.FileName)),
            snapshotFolder);

        var snapshot = BuildSnapshot(folderName, capturedAt, trigger, tag, report, slot.Source.Kind.ToString());
        manifest.Snapshots.Add(snapshot);
        ApplyRetention(manifest, snapshotsRoot);
        SnapshotIndex.Save(manifest, manifestPath);

        return new BackupResult { Success = true, Snapshot = snapshot };
    }

    public BackupResult SnapshotAccount(SaveAccount account, SnapshotTrigger trigger, string? tag = null)
    {
        var report = IntegrityChecker.InspectAccount(account);

        if (trigger == SnapshotTrigger.Auto && report.Health == SlotHealth.Corrupted)
            return new BackupResult { Success = false, Reason = "Account data is corrupted; auto-snapshot abandoned." };

        var snapshotsRoot = SnapshotStore.GetAccountSnapshotsRoot(_backupRoot, account.SteamId32);
        var manifestPath = SnapshotStore.GetManifestPath(snapshotsRoot);
        var manifest = SnapshotIndex.Load(manifestPath);

        var lastSnap = LatestSnapshot(manifest);
        if (lastSnap != null && lastSnap.CombinedHash == report.CombinedHash)
        {
            return new BackupResult
            {
                Success = true, Skipped = true, Snapshot = lastSnap,
                Reason = "No changes since last snapshot.",
            };
        }

        var capturedAt = DateTime.UtcNow;
        var folderName = BuildFolderName(capturedAt, report.CombinedHash);
        var snapshotFolder = Path.Combine(snapshotsRoot, folderName);

        SnapshotStore.CopyFilesAtomically(
            account.AccountFiles.Select(f => (f.FullPath, f.FileName)),
            snapshotFolder);

        var snapshot = BuildSnapshot(folderName, capturedAt, trigger, tag, report, account.Source.Kind.ToString());
        manifest.Snapshots.Add(snapshot);
        ApplyRetention(manifest, snapshotsRoot);
        SnapshotIndex.Save(manifest, manifestPath);

        return new BackupResult { Success = true, Snapshot = snapshot };
    }

    // Prune old auto snapshots from disk and update the manifest in-place to
    // hold only the kept entries. Deletes are best-effort: if a folder can't
    // be removed, the manifest still drops the reference so we don't keep a
    // dangling pointer.
    private void ApplyRetention(SnapshotManifest manifest, string snapshotsRoot)
    {
        var plan = RetentionPolicy.Apply(manifest.Snapshots, _retention, DateTime.UtcNow);
        foreach (var pruned in plan.Prune)
        {
            var folder = Path.Combine(snapshotsRoot, pruned.FolderName);
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
            catch { /* best effort */ }
        }
        manifest.Snapshots.Clear();
        foreach (var keep in plan.Keep) manifest.Snapshots.Add(keep);
    }

    public IReadOnlyList<Snapshot> ListSlotSnapshots(string steamId32, string accountFolder, string slotName)
    {
        var path = SnapshotStore.GetManifestPath(
            SnapshotStore.GetSlotSnapshotsRoot(_backupRoot, steamId32, accountFolder, slotName));
        return SnapshotIndex.Load(path).Snapshots;
    }

    public IReadOnlyList<Snapshot> ListAccountSnapshots(string steamId32)
    {
        var path = SnapshotStore.GetManifestPath(
            SnapshotStore.GetAccountSnapshotsRoot(_backupRoot, steamId32));
        return SnapshotIndex.Load(path).Snapshots;
    }

    // Tag updates: empty/whitespace clears the tag; otherwise the trimmed value
    // is stored. Returns true on a successful update, false if the snapshot id
    // wasn't in the manifest (which can happen if a concurrent prune ran).
    public bool UpdateSlotSnapshotTag(string steamId32, string accountFolder, string slotName, string snapshotId, string? newTag) =>
        UpdateTag(SnapshotStore.GetSlotSnapshotsRoot(_backupRoot, steamId32, accountFolder, slotName), snapshotId, newTag);

    public bool UpdateAccountSnapshotTag(string steamId32, string snapshotId, string? newTag) =>
        UpdateTag(SnapshotStore.GetAccountSnapshotsRoot(_backupRoot, steamId32), snapshotId, newTag);

    public bool DeleteSlotSnapshot(string steamId32, string accountFolder, string slotName, string snapshotId) =>
        DeleteSnapshotEntry(SnapshotStore.GetSlotSnapshotsRoot(_backupRoot, steamId32, accountFolder, slotName), snapshotId);

    public bool DeleteAccountSnapshot(string steamId32, string snapshotId) =>
        DeleteSnapshotEntry(SnapshotStore.GetAccountSnapshotsRoot(_backupRoot, steamId32), snapshotId);

    private static bool UpdateTag(string snapshotsRoot, string snapshotId, string? newTag)
    {
        var manifestPath = SnapshotStore.GetManifestPath(snapshotsRoot);
        var manifest = SnapshotIndex.Load(manifestPath);
        var snap = manifest.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snap == null) return false;
        snap.Tag = string.IsNullOrWhiteSpace(newTag) ? null : newTag.Trim();
        SnapshotIndex.Save(manifest, manifestPath);
        return true;
    }

    private static bool DeleteSnapshotEntry(string snapshotsRoot, string snapshotId)
    {
        var manifestPath = SnapshotStore.GetManifestPath(snapshotsRoot);
        var manifest = SnapshotIndex.Load(manifestPath);
        var snap = manifest.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snap == null) return false;

        var folder = Path.Combine(snapshotsRoot, snap.FolderName);
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch { /* best effort — manifest still drops the reference */ }

        manifest.Snapshots.Remove(snap);
        SnapshotIndex.Save(manifest, manifestPath);
        return true;
    }

    private static Snapshot? LatestSnapshot(SnapshotManifest manifest) =>
        manifest.Snapshots.Count == 0 ? null : manifest.Snapshots.OrderByDescending(s => s.CapturedAtUtc).First();

    private static Snapshot BuildSnapshot(
        string folderName,
        DateTime capturedAt,
        SnapshotTrigger trigger,
        string? tag,
        IntegrityReport report,
        string sourceKind) => new()
    {
        Id = folderName,
        CapturedAtUtc = capturedAt,
        Trigger = trigger,
        Tag = tag,
        CombinedHash = report.CombinedHash,
        FileHashes = report.FileResults.ToDictionary(f => f.FileName, f => f.Sha256),
        TotalBytes = report.TotalBytes,
        Health = report.Health,
        SourceKind = sourceKind,
        FolderName = folderName,
    };

    // Format: 2026-05-06T14-30-12Z__a1b2c3 (colon-free for Windows compatibility)
    private static string BuildFolderName(DateTime utc, string combinedHash)
    {
        var ts = utc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        const string prefix = "sha256:";
        var hashStart = combinedHash.StartsWith(prefix, StringComparison.Ordinal)
            ? prefix.Length : 0;
        var available = combinedHash.Length - hashStart;
        var shortHash = combinedHash.Substring(hashStart, Math.Min(6, available));
        return $"{ts}__{shortHash}";
    }
}
