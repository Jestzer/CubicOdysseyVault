using CubicOdysseyVault.UI.Services;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class AppSettingsServiceTests
{
    [Fact]
    public void LoadFromMissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-missing-{Guid.NewGuid():N}.json");
        var s = AppSettingsService.LoadFromFile(path);

        Assert.False(s.HasCompletedOnboarding);
        Assert.Equal("", s.BackupRootPath);
        Assert.Empty(s.ManualSourceRoots);
        Assert.True(s.WatcherEnabled);
        Assert.Equal(24, s.HourlySnapshotsKept);
        Assert.Equal(14, s.DailySnapshotsKept);
        Assert.Equal(8, s.WeeklySnapshotsKept);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-roundtrip-{Guid.NewGuid():N}.json");
        try
        {
            var original = new AppSettings
            {
                HasCompletedOnboarding = true,
                BackupRootPath = "/some/path",
                ManualSourceRoots = new List<string> { "/extra/saves", "/usb/saves" },
                WatcherEnabled = false,
                HourlySnapshotsKept = 48,
                DailySnapshotsKept = 30,
                WeeklySnapshotsKept = 12,
                WatcherDebounceSeconds = 20,
            };
            AppSettingsService.SaveToFile(original, path);

            var loaded = AppSettingsService.LoadFromFile(path);

            Assert.True(loaded.HasCompletedOnboarding);
            Assert.Equal("/some/path", loaded.BackupRootPath);
            Assert.Equal(new[] { "/extra/saves", "/usb/saves" }, loaded.ManualSourceRoots);
            Assert.False(loaded.WatcherEnabled);
            Assert.Equal(48, loaded.HourlySnapshotsKept);
            Assert.Equal(30, loaded.DailySnapshotsKept);
            Assert.Equal(12, loaded.WeeklySnapshotsKept);
            Assert.Equal(20, loaded.WatcherDebounceSeconds);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void LoadFromCorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-corrupt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ not valid json");
            var s = AppSettingsService.LoadFromFile(path);
            Assert.False(s.HasCompletedOnboarding);
            Assert.Equal("", s.BackupRootPath);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void Save_CreatesDirectoryAsNeeded()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"covtest-dir-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "settings.json");
        try
        {
            var s = new AppSettings { BackupRootPath = "/x" };
            AppSettingsService.SaveToFile(s, path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void GetSuggestedBackupRoot_ContainsAppName()
    {
        var path = AppSettingsService.GetSuggestedBackupRoot();
        Assert.Contains("CubicOdysseyVault", path);
    }

    [Fact]
    public void GetSuggestedBackupRoot_DoesNotIncludeRedundantSnapshotsSegment()
    {
        // SnapshotStore.GetSlotSnapshotsRoot appends "snapshots" itself, so the
        // suggested default must NOT include it — otherwise data lands at
        // <root>/snapshots/snapshots/... and the store enumerator misses it.
        var path = AppSettingsService.GetSuggestedBackupRoot();
        Assert.False(path.EndsWith($"{Path.DirectorySeparatorChar}snapshots", StringComparison.Ordinal),
            $"GetSuggestedBackupRoot returned '{path}', which ends with redundant 'snapshots' segment.");
    }
}
