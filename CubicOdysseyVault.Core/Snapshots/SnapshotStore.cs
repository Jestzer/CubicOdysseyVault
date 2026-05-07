namespace CubicOdysseyVault.Core.Snapshots;

public static class SnapshotStore
{
    public const string SnapshotsRootFolder = "snapshots";
    public const string AccountFolderSentinel = "_account";
    public const string ManifestFileName = "manifest.json";

    public static string GetSlotSnapshotsRoot(string backupRoot, string steamId32, string accountFolder, string slotName) =>
        Path.Combine(backupRoot, SnapshotsRootFolder, steamId32, accountFolder, slotName);

    public static string GetAccountSnapshotsRoot(string backupRoot, string steamId32) =>
        Path.Combine(backupRoot, SnapshotsRootFolder, steamId32, AccountFolderSentinel);

    public static string GetManifestPath(string snapshotsRoot) =>
        Path.Combine(snapshotsRoot, ManifestFileName);

    public static void CopyFilesAtomically(IEnumerable<(string SrcPath, string FileName)> files, string destFolder)
    {
        Directory.CreateDirectory(destFolder);
        foreach (var (src, name) in files)
        {
            var destFinal = Path.Combine(destFolder, name);
            var destTmp = destFinal + ".tmp";
            File.Copy(src, destTmp, overwrite: true);
            File.Move(destTmp, destFinal, overwrite: true);
        }
    }
}
