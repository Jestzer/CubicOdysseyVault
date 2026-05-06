namespace CubicOdysseyVault.Core.Steam;

public static class SteamLocator
{
    public static IReadOnlyList<SteamRoot> Locate() =>
        Locate(Array.Empty<string>());

    public static IReadOnlyList<SteamRoot> Locate(IEnumerable<string> manualOverrides)
    {
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var seen = new HashSet<string>(pathComparer);
        var roots = new List<SteamRoot>();

        foreach (var candidate in GetPlatformCandidateRoots())
        {
            var expanded = ExpandPath(candidate);
            if (string.IsNullOrEmpty(expanded) || !Directory.Exists(expanded)) continue;

            if (TryAddRoot(expanded, SteamRootSource.CandidatePath, seen, roots, out var canonical))
                AddLibraryFoldersFromRoot(canonical, seen, roots);
        }

        foreach (var registryPath in ReadWindowsRegistryPaths())
        {
            if (string.IsNullOrEmpty(registryPath) || !Directory.Exists(registryPath)) continue;
            if (TryAddRoot(registryPath, SteamRootSource.Registry, seen, roots, out var canonical))
                AddLibraryFoldersFromRoot(canonical, seen, roots);
        }

        foreach (var manual in manualOverrides ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(manual) || !Directory.Exists(manual)) continue;
            TryAddRoot(manual, SteamRootSource.Manual, seen, roots, out _);
        }

        return roots;
    }

    private static void AddLibraryFoldersFromRoot(string steamRoot, HashSet<string> seen, List<SteamRoot> roots)
    {
        var primary = Path.Combine(steamRoot, Constants.LibraryFoldersVdfRelative);
        var fallback = Path.Combine(steamRoot, Constants.LibraryFoldersVdfRelativeAlt);
        var vdfPath = File.Exists(primary) ? primary : File.Exists(fallback) ? fallback : null;
        if (vdfPath == null) return;

        IReadOnlyList<string> libPaths;
        try
        {
            libPaths = LibraryFoldersVdfParser.ParseLibraryPathsFromFile(vdfPath);
        }
        catch
        {
            return;
        }

        foreach (var lib in libPaths)
        {
            if (string.IsNullOrEmpty(lib) || !Directory.Exists(lib)) continue;
            TryAddRoot(lib, SteamRootSource.LibraryFoldersVdf, seen, roots, out _);
        }
    }

    private static bool TryAddRoot(
        string path,
        SteamRootSource source,
        HashSet<string> seen,
        List<SteamRoot> roots,
        out string canonical)
    {
        canonical = Canonicalize(path);
        if (!seen.Add(canonical))
            return false;
        roots.Add(new SteamRoot(path, canonical, source));
        return true;
    }

    private static IEnumerable<string> GetPlatformCandidateRoots()
    {
        if (OperatingSystem.IsWindows()) return Constants.WindowsSteamCandidateRoots;
        if (OperatingSystem.IsMacOS()) return Constants.MacSteamCandidateRoots;
        return Constants.LinuxSteamCandidateRoots;
    }

    private static IEnumerable<string> ReadWindowsRegistryPaths()
    {
        // TODO Phase 9: probe HKCU\Software\Valve\Steam\SteamPath via Microsoft.Win32.Registry.
        return Array.Empty<string>();
    }

    internal static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        if (OperatingSystem.IsWindows())
            path = Environment.ExpandEnvironmentVariables(path);

        if (path.StartsWith("~", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) return path;
            var rest = path.Length > 1 ? path[1..].TrimStart('/', '\\') : string.Empty;
            path = string.IsNullOrEmpty(rest) ? home : Path.Combine(home, rest);
        }

        return path;
    }

    // Returns the canonical absolute path with all symlinks (including intermediate
    // ones) resolved. .NET's ResolveLinkTarget only resolves the leaf, so we walk
    // each path segment and resolve when the segment itself is a link. Iterates a
    // few times because resolved targets can themselves contain symlinks.
    internal static string Canonicalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch { return path; }

        string current = fullPath;
        for (int iter = 0; iter < 16; iter++)
        {
            var resolved = ResolveSegments(current);
            if (string.Equals(resolved, current, StringComparison.Ordinal)) break;
            current = resolved;
        }

        return current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string ResolveSegments(string fullPath)
    {
        var sep = Path.DirectorySeparatorChar;
        var parts = fullPath.Split(new[] { sep, Path.AltDirectorySeparatorChar }, StringSplitOptions.None);

        string accumulator;
        int start;
        if (!OperatingSystem.IsWindows() && parts.Length > 0 && string.IsNullOrEmpty(parts[0]))
        {
            accumulator = sep.ToString();
            start = 1;
        }
        else
        {
            accumulator = parts[0];
            start = 1;
        }

        for (int i = start; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;

            var combined = Path.Combine(accumulator, parts[i]);

            try
            {
                var di = new DirectoryInfo(combined);
                if (di.Exists)
                {
                    var resolved = di.ResolveLinkTarget(returnFinalTarget: true);
                    accumulator = resolved?.FullName ?? di.FullName;
                    continue;
                }

                var fi = new FileInfo(combined);
                if (fi.Exists)
                {
                    var resolved = fi.ResolveLinkTarget(returnFinalTarget: true);
                    accumulator = resolved?.FullName ?? fi.FullName;
                    continue;
                }
            }
            catch { /* fall through to unresolved */ }

            accumulator = combined;
        }

        return accumulator;
    }
}
