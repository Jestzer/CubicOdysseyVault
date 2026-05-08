using System.Text.Json;

namespace CubicOdysseyVault.UI.Services;

public static class AppSettingsService
{
    private static string ConfigDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CubicOdysseyVault");

    private static string FilePath => Path.Combine(ConfigDir, "settings.json");

    public static AppSettings Load() => LoadFromFile(FilePath);

    public static void Save(AppSettings settings) => SaveToFile(settings, FilePath);

    public static AppSettings LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (Migrate(settings)) SaveToFile(settings, path);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    // Migrates known-bad legacy values to current ones. Currently only handles
    // the BackupRootPath that earlier `GetSuggestedBackupRoot()` versions
    // produced with a redundant trailing `/snapshots/` segment — which made
    // the program look one level too deep for snapshot data.
    private static bool Migrate(AppSettings settings)
    {
        var changed = false;
        var legacyDefault = LegacyGetSuggestedBackupRoot();
        if (string.Equals(settings.BackupRootPath, legacyDefault, StringComparison.Ordinal))
        {
            settings.BackupRootPath = GetSuggestedBackupRoot();
            changed = true;
        }
        return changed;
    }

    private static string LegacyGetSuggestedBackupRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CubicOdysseyVault",
            "snapshots");

    public static void SaveToFile(AppSettings settings, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Silently ignore write failures — matches OpenFATX/WindowSettingsService.
        }
    }

    // The directory the program treats as the parent of the on-disk store.
    // SnapshotStore appends a "snapshots" subfolder, so the suggested default
    // is the parent — NOT including "snapshots" — to avoid the data ending up
    // at <root>/snapshots/snapshots/...
    public static string GetSuggestedBackupRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CubicOdysseyVault");
}

public class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool HasCompletedOnboarding { get; set; }
    public string BackupRootPath { get; set; } = "";
    public List<string> ManualSourceRoots { get; set; } = new();
    public bool WatcherEnabled { get; set; } = true;
    public int HourlySnapshotsKept { get; set; } = 24;
    public int DailySnapshotsKept { get; set; } = 14;
    public int WeeklySnapshotsKept { get; set; } = 8;
    public int WatcherDebounceSeconds { get; set; } = 10;
    // Optional explicit path to the Cubic Odyssey install directory (the
    // folder containing data/configs/items/ and data/sprites/icons.png).
    // Empty = auto-discover under each Steam root's steamapps/common/.
    public string GameInstallPath { get; set; } = "";
}
