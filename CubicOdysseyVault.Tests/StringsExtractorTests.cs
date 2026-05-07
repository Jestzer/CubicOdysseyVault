using System.Linq;
using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class StringsExtractorTests
{
    [Fact]
    public void Extract_FindsAsciiRunsAtCorrectOffsets()
    {
        var data = System.Text.Encoding.ASCII.GetBytes("\0hello\0\0world!\0\0");
        var results = StringsExtractor.Extract(data, minLength: 4);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Offset);
        Assert.Equal("hello", results[0].Text);
        Assert.Equal(8, results[1].Offset);
        Assert.Equal("world!", results[1].Text);
    }

    [Fact]
    public void Extract_HonorsMinLength()
    {
        var data = System.Text.Encoding.ASCII.GetBytes("aa\0aaaa\0aaaaaa");
        var results = StringsExtractor.Extract(data, minLength: 4);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Text == "aaaa");
        Assert.Contains(results, r => r.Text == "aaaaaa");
    }

    [Fact]
    public void Extract_NonPrintableSplitsRuns()
    {
        var data = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x01, 0x45, 0x46, 0x47, 0x48 };
        var results = StringsExtractor.Extract(data, minLength: 4);
        Assert.Equal(2, results.Count);
        Assert.Equal("ABCD", results[0].Text);
        Assert.Equal("EFGH", results[1].Text);
    }

    [Fact]
    public void Extract_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(StringsExtractor.Extract(Array.Empty<byte>()));
    }

    [Fact]
    public void Extract_RunAtEndOfBufferIsCaptured()
    {
        var data = System.Text.Encoding.ASCII.GetBytes("\0\0trailing");
        var results = StringsExtractor.Extract(data, minLength: 4);
        Assert.Single(results);
        Assert.Equal("trailing", results[0].Text);
        Assert.Equal(2, results[0].Offset);
    }
}
