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
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

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

    public static string GetSuggestedBackupRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CubicOdysseyVault",
            "snapshots");
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
}
