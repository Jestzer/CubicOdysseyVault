using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Steam;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class SaveLocatorTests
{
    [Fact]
    public void DuplicateManualPaths_AreDeduplicated()
    {
        using var fixture = new TempLayoutFixture();
        var sources = SaveLocator.LocateSources(
            Array.Empty<SteamRoot>(),
            new[] { fixture.RootPath, fixture.RootPath });

        Assert.Single(sources);
        Assert.Equal(SaveSourceKind.Manual, sources[0].Kind);
    }

    [Fact]
    public void TrailingSlashVariants_AreDeduplicated()
    {
        using var fixture = new TempLayoutFixture();
        var withSlash = fixture.RootPath + Path.DirectorySeparatorChar;
        var sources = SaveLocator.LocateSources(
            Array.Empty<SteamRoot>(),
            new[] { fixture.RootPath, withSlash });

        Assert.Single(sources);
    }

    [Fact]
    public void SymlinkedManualPaths_AreDeduplicated()
    {
        // Mirrors the real scenario on this user's machine: 4 Steam libraries
        // each with compatdata symlinked to a single Proton prefix.
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return; // Symlink creation requires admin on Windows; skip there.

        using var fixture = new TempLayoutFixture();
        var linkPath = Path.Combine(Path.GetTempPath(), $"covtest-link-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateSymbolicLink(linkPath, fixture.RootPath);

            var sources = SaveLocator.LocateSources(
                Array.Empty<SteamRoot>(),
                new[] { fixture.RootPath, linkPath });

            Assert.Single(sources);
        }
        finally
        {
            try { File.Delete(linkPath); } catch { }
            try { Directory.Delete(linkPath); } catch { }
        }
    }

    [Fact]
    public void EmptyInputs_ReturnEmpty()
    {
        var sources = SaveLocator.LocateSources(Array.Empty<SteamRoot>());
        Assert.Empty(sources);
    }

    [Fact]
    public void NonExistentManualPath_StillRecorded_AsExistsFalse()
    {
        var bogus = Path.Combine(Path.GetTempPath(), $"covtest-missing-{Guid.NewGuid():N}");
        var sources = SaveLocator.LocateSources(
            Array.Empty<SteamRoot>(),
            new[] { bogus });

        Assert.Single(sources);
        Assert.False(sources[0].Exists);
    }
}
