using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class HexFormatterTests
{
    [Fact]
    public void Format_FullLine_HasOffsetHexAndAsciiPane()
    {
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x6C, 0x64, 0x21, 0, 0, 0, 0 };
        var lines = HexFormatter.Format(data);
        Assert.Single(lines);
        Assert.StartsWith("00000000  ", lines[0]);
        Assert.Contains("48 65 6c 6c 6f 20 77 6f", lines[0]);
        Assert.Contains("Hello world!", lines[0]);
    }

    [Fact]
    public void Format_PartialLastLine_PadsHexColumns()
    {
        var data = new byte[] { 0xAA, 0xBB, 0xCC };
        var lines = HexFormatter.Format(data);
        Assert.Single(lines);
        Assert.Contains("aa bb cc", lines[0]);
        Assert.EndsWith("...", lines[0]);
    }

    [Fact]
    public void Format_MultipleLines_OffsetsIncrementByBytesPerLine()
    {
        var data = new byte[40];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;
        var lines = HexFormatter.Format(data, bytesPerLine: 16);
        Assert.Equal(3, lines.Count);
        Assert.StartsWith("00000000", lines[0]);
        Assert.StartsWith("00000010", lines[1]);
        Assert.StartsWith("00000020", lines[2]);
    }

    [Fact]
    public void Format_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(HexFormatter.Format(Array.Empty<byte>()));
    }
}
