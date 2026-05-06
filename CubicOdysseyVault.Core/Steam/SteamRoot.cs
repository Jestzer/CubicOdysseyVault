namespace CubicOdysseyVault.Core.Steam;

public enum SteamRootSource
{
    CandidatePath,
    LibraryFoldersVdf,
    Registry,
    EnvOverride,
    Manual,
}

public sealed record SteamRoot(string Path, string CanonicalPath, SteamRootSource Source);
