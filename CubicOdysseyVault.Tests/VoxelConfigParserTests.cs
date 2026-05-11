using CubicOdysseyVault.Core.Voxels;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class VoxelConfigParserTests
{
    [Fact]
    public void ParsesRgbaFormat()
    {
        var text = """
            VoxelCfg
            {
                name             "Alien Grass 1"
                m_color          [208,137,118,255]
            }
            """;
        var color = VoxelConfigParser.ParseColorText(text);
        Assert.Equal(new VoxelConfigParser.Rgb(208, 137, 118), color);
    }

    [Fact]
    public void ParsesRgbWithoutAlpha()
    {
        var text = """
            VoxelCfg
            {
                m_color          [0,0,0]
            }
            """;
        var color = VoxelConfigParser.ParseColorText(text);
        Assert.Equal(new VoxelConfigParser.Rgb(0, 0, 0), color);
    }

    [Fact]
    public void ToleratesWhitespaceAndExtraSpaces()
    {
        var text = "m_color   [  10 ,  20  , 30 , 40 ]";
        var color = VoxelConfigParser.ParseColorText(text);
        Assert.Equal(new VoxelConfigParser.Rgb(10, 20, 30), color);
    }

    [Fact]
    public void ReturnsNullWhenColorMissing()
    {
        var text = """
            VoxelCfg
            {
                name "no color"
            }
            """;
        Assert.Null(VoxelConfigParser.ParseColorText(text));
    }

    [Fact]
    public void ReturnsNullForMalformedColor()
    {
        // Only two channels — not a valid color triple.
        Assert.Null(VoxelConfigParser.ParseColorText("m_color [1,2]"));
        // Non-numeric.
        Assert.Null(VoxelConfigParser.ParseColorText("m_color [a,b,c]"));
        // Out of byte range.
        Assert.Null(VoxelConfigParser.ParseColorText("m_color [256,0,0]"));
    }
}
