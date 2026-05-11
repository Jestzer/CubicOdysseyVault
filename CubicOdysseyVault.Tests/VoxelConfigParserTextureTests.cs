using CubicOdysseyVault.Core.Voxels;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class VoxelConfigParserTextureTests
{
    [Fact]
    public void ParsesTopAndSideTextures()
    {
        var text = """
            VoxelCfg
            {
                name                  "Wall Metal 7"
                m_defaultTexture      "wall_metal_7_side"
                m_defaultTextureTop   "wall_metal_7"
                m_color               [146,148,151,255]
            }
            """;
        var parsed = VoxelConfigParser.ParseText(text);
        Assert.Equal("wall_metal_7_side", parsed.DefaultTexture);
        Assert.Equal("wall_metal_7", parsed.DefaultTextureTop);
    }

    [Fact]
    public void DistinguishesDefaultTextureFromDefaultTextureTop()
    {
        // m_defaultTextureTop must NOT bleed into m_defaultTexture matches —
        // the prefix-disambiguation is the failure mode being guarded.
        var text = """
            m_defaultTextureTop   "only_top"
            """;
        var parsed = VoxelConfigParser.ParseText(text);
        Assert.Equal("only_top", parsed.DefaultTextureTop);
        Assert.Null(parsed.DefaultTexture);
    }

    [Fact]
    public void NoTextures_ReturnsNullsButKeepsColor()
    {
        var text = "m_color [10,20,30,255]";
        var parsed = VoxelConfigParser.ParseText(text);
        Assert.Equal(new VoxelConfigParser.Rgb(10, 20, 30), parsed.Color);
        Assert.Null(parsed.DefaultTexture);
        Assert.Null(parsed.DefaultTextureTop);
    }
}
