using CubicOdysseyVault.UI.Services;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class AppSettingsGameInstallTests
{
    [Fact]
    public void GameInstallPath_DefaultsToEmpty()
    {
        var s = new AppSettings();
        Assert.Equal("", s.GameInstallPath);
    }

    [Fact]
    public void GameInstallPath_RoundTripsThroughJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-gi-{Guid.NewGuid():N}.json");
        try
        {
            var original = new AppSettings { GameInstallPath = "/games/cubic-odyssey" };
            AppSettingsService.SaveToFile(original, path);
            var loaded = AppSettingsService.LoadFromFile(path);
            Assert.Equal("/games/cubic-odyssey", loaded.GameInstallPath);
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
