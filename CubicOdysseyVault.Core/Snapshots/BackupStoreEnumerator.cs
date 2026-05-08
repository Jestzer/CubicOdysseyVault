namespace CubicOdysseyVault.Core.Snapshots;

public sealed record BackupStoreLayout(
    IReadOnlyList<BackupStoreAccount> Accounts,
    IReadOnlyList<BackupStoreSlot> Slots);

public sealed record BackupStoreAccount(string SteamId32);

public sealed record BackupStoreSlot(string SteamId32, string AccountFolderName, string SlotName);

// Walks the backup store under <backupRoot>/snapshots/ and reports which
// SteamID32 / account / slot combinations have backups on disk. Used to
// surface "orphan" backups — entries that exist in the store but not in
// any live save source.
//
// The store layout is:
//   <backupRoot>/snapshots/<SteamID32>/_account/manifest.json + <ts>__<hash>/
//   <backupRoot>/snapshots/<SteamID32>/<account>/<slot>/manifest.json + <ts>__<hash>/
//
// We only report entries that have a manifest.json — folders missing a
// manifest are treated as malformed and skipped. The check keeps the
// reporting conservative so we don't fabricate users from random debris.
public static class BackupStoreEnumerator
{
    public static BackupStoreLayout Enumerate(string backupRoot)
    {
        var accounts = new List<BackupStoreAccount>();
        var slots = new List<BackupStoreSlot>();

        var snapshotsRoot = Path.Combine(backupRoot, SnapshotStore.SnapshotsRootFolder);
        if (!Directory.Exists(snapshotsRoot))
            return new BackupStoreLayout(accounts, slots);

        IEnumerable<string> steamIdDirs;
        try { steamIdDirs = Directory.EnumerateDirectories(snapshotsRoot); }
        catch (UnauthorizedAccessException) { return new BackupStoreLayout(accounts, slots); }

        foreach (var steamIdDir in steamIdDirs)
        {
            var steamId = Path.GetFileName(steamIdDir);
            if (!IsAllDigits(steamId)) continue;

            var accountManifestPath = Path.Combine(steamIdDir, SnapshotStore.AccountFolderSentinel, SnapshotStore.ManifestFileName);
            if (File.Exists(accountManifestPath))
                accounts.Add(new BackupStoreAccount(steamId));

            IEnumerable<string> accountDirs;
            try { accountDirs = Directory.EnumerateDirectories(steamIdDir); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var accountDir in accountDirs)
            {
                var accountName = Path.GetFileName(accountDir);
                if (string.Equals(accountName, SnapshotStore.AccountFolderSentinel, StringComparison.Ordinal))
                    continue;

                IEnumerable<string> slotDirs;
                try { slotDirs = Directory.EnumerateDirectories(accountDir); }
                catch (UnauthorizedAccessException) { continue; }

                foreach (var slotDir in slotDirs)
                {
                    var slotName = Path.GetFileName(slotDir);
                    var slotManifestPath = Path.Combine(slotDir, SnapshotStore.ManifestFileName);
                    if (File.Exists(slotManifestPath))
                        slots.Add(new BackupStoreSlot(steamId, accountName, slotName));
                }
            }
        }

        return new BackupStoreLayout(accounts, slots);
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }
}
