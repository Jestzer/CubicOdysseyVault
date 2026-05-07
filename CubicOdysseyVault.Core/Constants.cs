namespace CubicOdysseyVault.Core;

public static class Constants
{
    public const int CubicOdysseyAppId = 3400000;
    public const string CubicOdysseySaveFolderName = "Cubic Odyssey";
    public const string CubicOdysseyProcessName = "CubicOdysseySteam";

    public const string LibraryFoldersVdfRelative = "steamapps/libraryfolders.vdf";
    public const string LibraryFoldersVdfRelativeAlt = "config/libraryfolders.vdf";
    public const string ProtonCompatdataRelative = "steamapps/compatdata";
    public const string ProtonDocumentsSubpath = "pfx/drive_c/users/steamuser/Documents";
    public const string SteamUserdataRelative = "userdata";
    public const string SteamCloudRemoteFolderName = "remote";
    public const string SteamCommonRelative = "steamapps/common";
    // The game's install directory under <SteamRoot>/steamapps/common/.
    public const string CubicOdysseyInstallFolderName = "Cubic Odyssey";

    public static readonly string[] LinuxSteamCandidateRoots =
    {
        "~/.steam/steam",
        "~/.local/share/Steam",
        "~/.var/app/com.valvesoftware.Steam/data/Steam",
    };

    public static readonly string[] WindowsSteamCandidateRoots =
    {
        @"%PROGRAMFILES(X86)%\Steam",
        @"%PROGRAMFILES%\Steam",
        @"C:\Steam",
        @"D:\Steam",
    };

    public static readonly string[] MacSteamCandidateRoots =
    {
        "~/Library/Application Support/Steam",
    };
}
