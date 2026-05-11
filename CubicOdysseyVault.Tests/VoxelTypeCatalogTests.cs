using System;
using System.IO;
using CubicOdysseyVault.Core.Voxels;
using Xunit;

namespace CubicOdysseyVault.Tests;

// Builds a synthetic <gameInstall>/data/configs/voxels/ tree and asserts the
// catalog maps voxels.lst lines (1-based) to their cfg's m_color.
public class VoxelTypeCatalogTests : IDisposable
{
    private readonly string _tempInstallDir;
    private readonly string _voxelsDir;

    public VoxelTypeCatalogTests()
    {
        _tempInstallDir = Path.Combine(Path.GetTempPath(), $"co-vault-vox-{Guid.NewGuid():N}");
        _voxelsDir = Path.Combine(_tempInstallDir, "data", "configs", "voxels");
        Directory.CreateDirectory(_voxelsDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempInstallDir, recursive: true); } catch { }
    }

    [Fact]
    public void Empty_WhenInstallDirMissing()
    {
        var catalog = VoxelTypeCatalog.LoadFrom("/no/such/path");
        Assert.True(catalog.IsEmpty);
    }

    [Fact]
    public void Empty_WhenVoxelsListMissing()
    {
        // Install dir exists but no voxels.lst → empty.
        var catalog = VoxelTypeCatalog.LoadFrom(_tempInstallDir);
        Assert.True(catalog.IsEmpty);
    }

    [Fact]
    public void MapsHighByteToCfgLineUsingOneBasedIndex()
    {
        WriteVoxelCfg("dev",       r: 1,   g: 2,   b: 3);
        WriteVoxelCfg("alien_grass6", r: 100, g: 200, b: 50);
        WriteVoxelCfg("wall_wood", r: 90, g: 60, b: 30);
        // voxels.lst order matters: line 1 → 0x01, line 93 → 0x5D, line 211 → 0xD3.
        var lines = new string[211];
        for (int i = 0; i < lines.Length; i++) lines[i] = "filler";
        lines[0]   = "dev";          // line 1 → 0x01
        lines[92]  = "alien_grass6"; // line 93 → 0x5D
        lines[210] = "wall_wood";    // line 211 → 0xD3
        File.WriteAllLines(Path.Combine(_voxelsDir, "voxels.lst"), lines);

        var catalog = VoxelTypeCatalog.LoadFrom(_tempInstallDir);

        var dev = catalog.Lookup(0x01);
        Assert.NotNull(dev);
        Assert.Equal("dev", dev!.Identifier);
        Assert.Equal(new VoxelDefinition("dev", 1, 2, 3), dev);

        var grass = catalog.Lookup(0x5D);
        Assert.NotNull(grass);
        Assert.Equal(new VoxelDefinition("alien_grass6", 100, 200, 50), grass);

        var wallWood = catalog.Lookup(0xD3);
        Assert.NotNull(wallWood);
        Assert.Equal(new VoxelDefinition("wall_wood", 90, 60, 30), wallWood);
    }

    [Fact]
    public void SkipsLinesWithMissingCfgFile()
    {
        // voxels.lst line 1 references a cfg that doesn't exist on disk.
        File.WriteAllLines(Path.Combine(_voxelsDir, "voxels.lst"), new[] { "missing_cfg" });
        var catalog = VoxelTypeCatalog.LoadFrom(_tempInstallDir);
        Assert.True(catalog.IsEmpty);
    }

    [Fact]
    public void SkipsLinesWhereCfgHasNoColor()
    {
        File.WriteAllLines(Path.Combine(_voxelsDir, "voxels.lst"), new[] { "no_color_voxel" });
        File.WriteAllText(
            Path.Combine(_voxelsDir, "no_color_voxel.cfg"),
            "VoxelCfg\n{\n    name \"no color\"\n}\n");
        var catalog = VoxelTypeCatalog.LoadFrom(_tempInstallDir);
        Assert.True(catalog.IsEmpty);
    }

    [Fact]
    public void ResolvesCfgFilenameCaseInsensitively()
    {
        // voxels.lst uses one case; the cfg file on disk uses another.
        // Mirrors the real install where voxels.lst has "Leaves_1" but the
        // file is "leaves_1.cfg" (or vice versa).
        File.WriteAllLines(Path.Combine(_voxelsDir, "voxels.lst"), new[] { "Leaves_1" });
        File.WriteAllText(
            Path.Combine(_voxelsDir, "leaves_1.cfg"),
            "m_color [11,22,33,255]");
        var catalog = VoxelTypeCatalog.LoadFrom(_tempInstallDir);
        var def = catalog.Lookup(0x01);
        Assert.NotNull(def);
        Assert.Equal(new VoxelDefinition("Leaves_1", 11, 22, 33), def);
    }

    [Fact]
    public void AutoDiscover_ReturnsFirstNonEmpty()
    {
        // First candidate is empty; second is our populated dir.
        var emptyDir = Path.Combine(Path.GetTempPath(), $"co-vault-vox-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);
        try
        {
            WriteVoxelCfg("dev", r: 1, g: 2, b: 3);
            File.WriteAllLines(Path.Combine(_voxelsDir, "voxels.lst"), new[] { "dev" });

            var catalog = VoxelTypeCatalog.AutoDiscover(new[] { emptyDir, _tempInstallDir });
            Assert.False(catalog.IsEmpty);
            Assert.Equal(_tempInstallDir, catalog.GameInstallDir);
        }
        finally
        {
            try { Directory.Delete(emptyDir, recursive: true); } catch { }
        }
    }

    private void WriteVoxelCfg(string name, byte r, byte g, byte b)
    {
        File.WriteAllText(
            Path.Combine(_voxelsDir, name + ".cfg"),
            $"VoxelCfg\n{{\n    name \"{name}\"\n    m_color [{r},{g},{b},255]\n}}\n");
    }
}
