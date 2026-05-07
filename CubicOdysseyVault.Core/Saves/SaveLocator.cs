using CubicOdysseyVault.Core.Steam;

namespace CubicOdysseyVault.Core.Saves;

public static class SaveLocator
{
    public static IReadOnlyList<SaveSource> LocateSources(IEnumerable<SteamRoot> steamRoots) =>
        LocateSources(steamRoots, Array.Empty<string>());

    public static IReadOnlyList<SaveSource> LocateSources(
        IEnumerable<SteamRoot> steamRoots,
        IEnumerable<string> manualSourceRoots)
    {
        var sources = new List<SaveSource>();
        var rootList = (steamRoots ?? Enumerable.Empty<SteamRoot>()).ToList();

        foreach (var root in rootList)
            AddProtonCompatdataSource(root, sources);

        foreach (var root in rootList)
            AddSteamCloudRemoteSources(root, sources);

        AddDocumentsSource(sources);

        foreach (var manual in manualSourceRoots ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(manual)) continue;
            sources.Add(new SaveSource(
                SaveSourceKind.Manual,
                manual,
                OriginatingSteamRoot: null,
                Exists: Directory.Exists(manual)));
        }

        return DedupByCanonicalPath(sources);
    }

    // Dedupe sources whose RootPaths resolve to the same physical directory. Steam
    // libraries on different drives commonly symlink compatdata to a single Proton
    // prefix, so multiple SteamRoots can produce ostensibly-different paths that
    // are the same data. First seen wins (preserves preferred-order: Proton, Cloud,
    // Documents, Manual).
    private static IReadOnlyList<SaveSource> DedupByCanonicalPath(List<SaveSource> sources)
    {
        var seen = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var result = new List<SaveSource>();
        foreach (var s in sources)
        {
            var canonical = SteamLocator.Canonicalize(s.RootPath);
            if (seen.Add(canonical))
                result.Add(s);
        }
        return result;
    }

    private static void AddProtonCompatdataSource(SteamRoot root, List<SaveSource> sources)
    {
        if (!OperatingSystem.IsLinux()) return;

        var gameRoot = Path.Combine(
            root.CanonicalPath,
            Constants.ProtonCompatdataRelative,
            Constants.CubicOdysseyAppId.ToString(),
            Constants.ProtonDocumentsSubpath,
            Constants.CubicOdysseySaveFolderName);

        var saveDir = Path.Combine(gameRoot, "save");

        if (Directory.Exists(saveDir))
        {
            sources.Add(new SaveSource(SaveSourceKind.ProtonCompatdata, saveDir, root, Exists: true));
        }
        else if (Directory.Exists(gameRoot))
        {
            sources.Add(new SaveSource(SaveSourceKind.ProtonCompatdata, saveDir, root, Exists: false));
        }
    }

    private static void AddSteamCloudRemoteSources(SteamRoot root, List<SaveSource> sources)
    {
        var userdataRoot = Path.Combine(root.CanonicalPath, Constants.SteamUserdataRelative);
        if (!Directory.Exists(userdataRoot)) return;

        IEnumerable<string> userDirs;
        try { userDirs = Directory.EnumerateDirectories(userdataRoot); }
        catch (UnauthorizedAccessException) { return; }

        foreach (var userDir in userDirs)
        {
            var appDir = Path.Combine(userDir, Constants.CubicOdysseyAppId.ToString());
            if (!Directory.Exists(appDir)) continue;

            // Inferred from remotecache.vdf paths like "Cubic Odyssey/save/<id>/0/2/screenshot.tga".
            // The remote/ subdirectory may not exist yet if Cloud sync hasn't run.
            var remoteSavesRoot = Path.Combine(
                appDir,
                Constants.SteamCloudRemoteFolderName,
                Constants.CubicOdysseySaveFolderName,
                "save");

            sources.Add(new SaveSource(
                SaveSourceKind.SteamCloudRemote,
                remoteSavesRoot,
                root,
                Exists: Directory.Exists(remoteSavesRoot)));
        }
    }

    private static void AddDocumentsSource(List<SaveSource> sources)
    {
        if (!OperatingSystem.IsWindows()) return;

        // .NET's GetFolderPath(MyDocuments) wraps SHGetKnownFolderPath, which
        // follows OneDrive's Known Folder Move when the user has Backup-PC
        // enabled — this is the canonical "Documents" path the game should
        // be writing to.
        AddDocumentsCandidate(sources,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        // Defensive fallbacks: a few games ignore the redirect and write to
        // %USERPROFILE%\Documents directly; some users have both layouts on
        // disk (game data was in the old location before OneDrive Backup-PC
        // got switched on, then half migrated). The dedup pass at the end of
        // LocateSources collapses duplicates by canonical path.
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            AddDocumentsCandidate(sources, Path.Combine(userProfile, "Documents"));
            AddDocumentsCandidate(sources, Path.Combine(userProfile, "OneDrive", "Documents"));
        }
    }

    private static void AddDocumentsCandidate(List<SaveSource> sources, string? documentsPath)
    {
        if (string.IsNullOrEmpty(documentsPath)) return;

        var gameRoot = Path.Combine(documentsPath, Constants.CubicOdysseySaveFolderName);
        var saveDir = Path.Combine(gameRoot, "save");

        if (Directory.Exists(saveDir))
        {
            sources.Add(new SaveSource(SaveSourceKind.Documents, saveDir, OriginatingSteamRoot: null, Exists: true));
        }
        else if (Directory.Exists(gameRoot))
        {
            // The game has launched once but `save/` hasn't been written yet.
            sources.Add(new SaveSource(SaveSourceKind.Documents, saveDir, OriginatingSteamRoot: null, Exists: false));
        }
    }
}
