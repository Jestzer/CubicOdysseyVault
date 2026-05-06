using CubicOdysseyVault.Core.Steam;

namespace CubicOdysseyVault.Core.Saves;

public enum SaveSourceKind
{
    ProtonCompatdata,
    Documents,
    SteamCloudRemote,
    Manual,
}

public sealed record SaveSource(
    SaveSourceKind Kind,
    string RootPath,
    SteamRoot? OriginatingSteamRoot,
    bool Exists);
