namespace CubicOdysseyVault.Core.Saves;

public static class SaveSlotEnumerator
{
    public static SaveLayout Enumerate(SaveSource source)
    {
        if (!source.Exists || !Directory.Exists(source.RootPath))
            return new SaveLayout(Array.Empty<SaveAccount>(), Array.Empty<SaveSlot>());

        var accounts = new List<SaveAccount>();
        var slots = new List<SaveSlot>();

        IEnumerable<string> steamIdDirs;
        try
        {
            steamIdDirs = Directory.EnumerateDirectories(source.RootPath);
        }
        catch (UnauthorizedAccessException) { return new SaveLayout(accounts, slots); }
        catch (DirectoryNotFoundException) { return new SaveLayout(accounts, slots); }

        foreach (var steamIdDir in steamIdDirs)
        {
            var steamId = Path.GetFileName(steamIdDir);
            if (!IsAllDigits(steamId)) continue;
            EnumerateSteamId(steamIdDir, steamId, source, accounts, slots);
        }

        return new SaveLayout(accounts, slots);
    }

    private static void EnumerateSteamId(
        string steamIdDir,
        string steamId,
        SaveSource source,
        List<SaveAccount> accounts,
        List<SaveSlot> slots)
    {
        var accountFiles = new List<SaveAccountFile>();
        long accountTotalBytes = 0;
        DateTime accountLastWrite = DateTime.MinValue;

        IEnumerable<string> filePaths;
        try { filePaths = Directory.EnumerateFiles(steamIdDir); }
        catch (UnauthorizedAccessException) { filePaths = Array.Empty<string>(); }

        foreach (var filePath in filePaths)
        {
            FileInfo info;
            try { info = new FileInfo(filePath); }
            catch { continue; }
            if (!info.Exists) continue;

            var lastWrite = info.LastWriteTimeUtc;
            accountFiles.Add(new SaveAccountFile(info.Name, info.FullName, info.Length, lastWrite));
            accountTotalBytes += info.Length;
            if (lastWrite > accountLastWrite) accountLastWrite = lastWrite;
        }

        if (accountLastWrite == DateTime.MinValue)
        {
            try { accountLastWrite = Directory.GetLastWriteTimeUtc(steamIdDir); }
            catch { accountLastWrite = DateTime.UtcNow; }
        }

        accounts.Add(new SaveAccount(
            steamId,
            steamIdDir,
            accountFiles,
            source,
            accountLastWrite,
            accountTotalBytes));

        IEnumerable<string> accountFolders;
        try { accountFolders = Directory.EnumerateDirectories(steamIdDir); }
        catch (UnauthorizedAccessException) { return; }

        foreach (var accountFolder in accountFolders)
        {
            var accountFolderName = Path.GetFileName(accountFolder);

            IEnumerable<string> slotFolders;
            try { slotFolders = Directory.EnumerateDirectories(accountFolder); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var slotFolder in slotFolders)
                EnumerateSlot(slotFolder, steamId, accountFolderName, source, slots);
        }
    }

    private static void EnumerateSlot(
        string slotFolder,
        string steamId,
        string accountFolderName,
        SaveSource source,
        List<SaveSlot> slots)
    {
        var slotName = Path.GetFileName(slotFolder);

        var slotFiles = new List<SaveSlotFile>();
        long totalBytes = 0;
        DateTime lastWrite = DateTime.MinValue;
        bool hasScreenshot = false;

        IEnumerable<string> filePaths;
        try { filePaths = Directory.EnumerateFiles(slotFolder); }
        catch (UnauthorizedAccessException) { return; }

        foreach (var filePath in filePaths)
        {
            FileInfo info;
            try { info = new FileInfo(filePath); }
            catch { continue; }
            if (!info.Exists) continue;

            var fileLastWrite = info.LastWriteTimeUtc;
            slotFiles.Add(new SaveSlotFile(info.Name, info.FullName, info.Length, fileLastWrite));
            totalBytes += info.Length;
            if (fileLastWrite > lastWrite) lastWrite = fileLastWrite;
            if (string.Equals(info.Name, "screenshot.tga", StringComparison.OrdinalIgnoreCase))
                hasScreenshot = true;
        }

        if (lastWrite == DateTime.MinValue)
        {
            try { lastWrite = Directory.GetLastWriteTimeUtc(slotFolder); }
            catch { lastWrite = DateTime.UtcNow; }
        }

        slots.Add(new SaveSlot(
            steamId,
            accountFolderName,
            slotName,
            slotFolder,
            slotFiles,
            hasScreenshot,
            source,
            lastWrite,
            totalBytes));
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }
}
