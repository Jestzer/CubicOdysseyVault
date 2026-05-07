using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class TlvParserTests
{
    [Fact]
    public void Parse_TwoInt32Entries_RoundTrips()
    {
        // Mirrors the shape of the real meta.sav: header + count=2 + two int32 entries.
        var bytes = new byte[]
        {
            0x08, 0x00, 0x00, 0x00,  // header
            0x02, 0x00,              // count = 2
            // entry 1: tag=1 type=4 length=4 value=2
            0x01, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
            // entry 2: tag=2 type=4 length=4 value=0
            0x02, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };

        var doc = TlvParser.Parse(bytes);

        Assert.Null(doc.ParseError);
        Assert.Equal(8, doc.HeaderTag);
        Assert.Equal(2, doc.Entries.Count);
        Assert.Equal(1, doc.Entries[0].Tag);
        Assert.Equal(TlvValueKind.Int32, doc.Entries[0].Kind);
        Assert.Equal(4, doc.Entries[0].RawData.Length);
        Assert.Equal(2, doc.Entries[1].Tag);
    }

    [Fact]
    public void Parse_ListType_RecursesIntoNestedEntries()
    {
        // header + count=1 + entry(tag=1 type=23 length=12 [count=1 + tag=2 type=4 len=4 val=42])
        var bytes = new byte[]
        {
            0x08, 0x00, 0x00, 0x00,  // header
            0x01, 0x00,              // outer count = 1
            // outer entry: tag=1 type=0x17 (list) length=14 nested-data
            0x01, 0x00, 0x17, 0x00, 0x0E, 0x00, 0x00, 0x00,
            // nested:
            0x01, 0x00,              // nested count = 1
            // nested entry: tag=2 type=4 length=4 value=42
            0x02, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00, 0x2A, 0x00, 0x00, 0x00,
        };

        var doc = TlvParser.Parse(bytes);
        Assert.Null(doc.ParseError);
        Assert.Single(doc.Entries);
        var outer = doc.Entries[0];
        Assert.Equal(TlvValueKind.List, outer.Kind);
        Assert.NotNull(outer.Nested);
        Assert.Single(outer.Nested!);
        var nested = outer.Nested![0];
        Assert.Equal(2, nested.Tag);
        Assert.Equal(TlvValueKind.Int32, nested.Kind);
        Assert.Equal(0x2A, nested.RawData[0]);
    }

    [Fact]
    public void Parse_FloatAndDoubleAndInt64_AreClassified()
    {
        var bytes = new byte[]
        {
            0x08, 0x00, 0x00, 0x00,  // header
            0x03, 0x00,              // count = 3
            // entry 1: tag=1 type=10 (float32) length=4
            0x01, 0x00, 0x0A, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F,  // 1.0f LE
            // entry 2: tag=2 type=11 (float64) length=8
            0x02, 0x00, 0x0B, 0x00, 0x08, 0x00, 0x00, 0x00, 0, 0, 0, 0, 0, 0, 0xF0, 0x3F, // 1.0 LE
            // entry 3: tag=3 type=9 (int64) length=8
            0x03, 0x00, 0x09, 0x00, 0x08, 0x00, 0x00, 0x00, 0x05, 0, 0, 0, 0, 0, 0, 0,
        };

        var doc = TlvParser.Parse(bytes);
        Assert.Null(doc.ParseError);
        Assert.Equal(3, doc.Entries.Count);
        Assert.Equal(TlvValueKind.Float32, doc.Entries[0].Kind);
        Assert.Equal(TlvValueKind.Float64, doc.Entries[1].Kind);
        Assert.Equal(TlvValueKind.Int64, doc.Entries[2].Kind);
    }

    [Fact]
    public void Parse_TooShortBuffer_ReturnsParseError()
    {
        var doc = TlvParser.Parse(new byte[] { 1, 2, 3 });
        Assert.NotNull(doc.ParseError);
    }

    [Fact]
    public void Parse_DeclaredLengthExceedsBuffer_ReturnsParseError_WithEntriesSoFar()
    {
        // header + count=2 + complete first entry + truncated second entry header
        var bytes = new byte[]
        {
            0x08, 0x00, 0x00, 0x00,
            0x02, 0x00,
            0x01, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x04, 0x00, 0xFF, 0xFF, 0x00, 0x00,  // declares 65535 bytes payload
        };
        var doc = TlvParser.Parse(bytes);
        Assert.NotNull(doc.ParseError);
        Assert.Single(doc.Entries); // first entry survived
    }

    [Fact]
    public void Parse_UnknownType_ClassifiedAsUnknown_RawDataPreserved()
    {
        var bytes = new byte[]
        {
            0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x01, 0x00, 0xFF, 0x00, 0x03, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC,
        };
        var doc = TlvParser.Parse(bytes);
        Assert.Null(doc.ParseError);
        Assert.Single(doc.Entries);
        Assert.Equal(TlvValueKind.Unknown, doc.Entries[0].Kind);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, doc.Entries[0].RawData);
    }
}
