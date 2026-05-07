using System.Linq;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.Core.Restore;

// Restore a slot to a previous snapshot's state. The flow:
//   1. Refuse if Cubic Odyssey is running (would race the swap).
//   2. Take a PreRestore snapshot of the live state — the restore is
//      undoable through the snapshot history.
//   3. Stage the snapshot's files into a sibling `<slot>.restore-tmp/`
//      folder (each file copied via `*.tmp` then renamed).
//   4. Atomic swap: live slot → `<slot>.replaced-<utc>/`, then staging →
//      live slot. If the second move fails, the original is moved back.
//   5. Cleanup any older `<slot>.replaced-*` folders before the new one
//      so we keep one generation of overwrite history (per PLAN.md).
public sealed class RestoreService
{
    private readonly string _backupRoot;
    private readonly Func<bool> _isGameRunning;

    public RestoreService(string backupRoot, Func<bool>? isGameRunning = null)
    {
        _backupRoot = backupRoot;
        _isGameRunning = isGameRunning ?? GameProcessDetector.IsCubicOdysseyRunning;
    }

    public RestoreResult RestoreSlot(SaveSlot slot, Snapshot snapshot, BackupService backupService)
    {
        if (_isGameRunning())
        {
            return new RestoreResult
            {
                Success = false,
                BlockedByRunningGame = true,
                Reason = "Cubic Odyssey is currently running. Close the game and try again.",
            };
        }

        var preResult = backupService.SnapshotSlot(slot, SnapshotTrigger.PreRestore, "Pre-restore");
        if (!preResult.Success)
        {
            return new RestoreResult
            {
                Success = false,
                Reason = $"Pre-restore snapshot failed: {preResult.Reason}",
            };
        }

        var snapshotsRoot = SnapshotStore.GetSlotSnapshotsRoot(
            _backupRoot, slot.SteamId32, slot.AccountFolderName, slot.SlotName);
        var snapshotFolder = Path.Combine(snapshotsRoot, snapshot.FolderName);
        if (!Directory.Exists(snapshotFolder))
        {
            return new RestoreResult
            {
                Success = false,
                Reason = "Snapshot folder no longer exists in the backup store.",
                PreRestoreSnapshot = preResult.Snapshot,
            };
        }

        var slotPath = slot.SlotFolderPath;
        if (!Directory.Exists(slotPath))
        {
            return new RestoreResult
            {
                Success = false,
                Reason = "Live slot folder is missing.",
                PreRestoreSnapshot = preResult.Snapshot,
            };
        }

        CleanupPreviousReplaced(slotPath);

        var stagingPath = slotPath + ".restore-tmp";
        var replacedPath = slotPath + $".replaced-{DateTime.UtcNow:yyyy-MM-ddTHH-mm-ssZ}";

        try
        {
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);

            SnapshotStore.CopyFilesAtomically(
                Directory.EnumerateFiles(snapshotFolder)
                    .Select(p => (SrcPath: p, FileName: Path.GetFileName(p))),
                stagingPath);

            Directory.Move(slotPath, replacedPath);
            try
            {
                Directory.Move(stagingPath, slotPath);
            }
            catch
            {
                try { if (!Directory.Exists(slotPath)) Directory.Move(replacedPath, slotPath); }
                catch { /* best effort rollback */ }
                throw;
            }

            return new RestoreResult
            {
                Success = true,
                PreRestoreSnapshot = preResult.Snapshot,
                ReplacedFolderPath = replacedPath,
            };
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, recursive: true); }
            catch { }

            return new RestoreResult
            {
                Success = false,
                Reason = ex.Message,
                PreRestoreSnapshot = preResult.Snapshot,
            };
        }
    }

    private static void CleanupPreviousReplaced(string slotPath)
    {
        var parent = Path.GetDirectoryName(slotPath);
        if (string.IsNullOrEmpty(parent)) return;
        var slotName = Path.GetFileName(slotPath);
        if (string.IsNullOrEmpty(slotName)) return;

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(parent))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith(slotName + ".replaced-", StringComparison.Ordinal))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
                }
            }
        }
        catch { /* parent unavailable */ }
    }
}
