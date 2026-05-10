using System.Buffers.Binary;
using CubicOdysseyVault.Core.Voxels;
using Xunit;
using ZstdSharp;

namespace CubicOdysseyVault.Tests;

public class WorldChunkReaderTests
{
    [Fact]
    public void Read_SyntheticChunk_RoundTripsVoxels()
    {
        // Build a chunk with 3 known voxels. Voxel Y reconstructs as
        // section*32 + offset where section is the file's byte 3 and offset
        // is byte 1 (5-bit each).
        var voxels = new[]
        {
            (X: (byte)0,   YOffset: (byte)1,  Z: (byte)2,   YSection: (byte)0,  BlockId: 0xCB02u),
            (X: (byte)128, YOffset: (byte)16, Z: (byte)200, YSection: (byte)5,  BlockId: 0xD500u),
            (X: (byte)255, YOffset: (byte)31, Z: (byte)128, YSection: (byte)30, BlockId: 0x000Bu),
        };
        var bytes = BuildChunk(voxels);
        var chunk = WorldChunkReader.ReadFromBytes(bytes, chunkId: 0x300000004UL);

        Assert.Equal(0x300000004UL, chunk.ChunkId);
        Assert.Equal(voxels.Length, chunk.Header.RecordCount);
        Assert.Equal(0xC0, chunk.Header.Flags);
        Assert.Equal(0x2u, chunk.Header.Version);
        Assert.Collection(chunk.Voxels,
            v => Assert.Equal(new Voxel(0,   1   + 0   * 32, 2,   0xCB02u), v),
            v => Assert.Equal(new Voxel(128, 16  + 5   * 32, 200, 0xD500u), v),
            v => Assert.Equal(new Voxel(255, 31  + 30  * 32, 128, 0x000Bu), v));
    }

    [Fact]
    public void Read_ZeroRecords_ReturnsEmpty()
    {
        var bytes = BuildChunk(Array.Empty<(byte, byte, byte, byte, uint)>());
        var chunk = WorldChunkReader.ReadFromBytes(bytes, chunkId: 0x300000004UL);
        Assert.Empty(chunk.Voxels);
        Assert.Equal(0, chunk.Header.RecordCount);
    }

    [Fact]
    public void Read_FileTooShort_Throws()
    {
        Assert.Throws<InvalidDataException>(() =>
            WorldChunkReader.ReadFromBytes(new byte[10], chunkId: 0));
    }

    [Fact]
    public void Read_BodyMissingZstdMagic_Throws()
    {
        var bytes = new byte[24];
        // Header is mostly zero; body bytes are also zero, no zstd magic.
        Assert.Throws<InvalidDataException>(() =>
            WorldChunkReader.ReadFromBytes(bytes, chunkId: 0));
    }

    [Fact]
    public void Read_BodyDecodesToWrongSize_Throws()
    {
        // Build a chunk that claims 3 records but compress only 2 records' worth.
        var truthful = BuildChunk(new[]
        {
            (X: (byte)1, YOffset: (byte)1, Z: (byte)1, YSection: (byte)0, BlockId: 0u),
            (X: (byte)2, YOffset: (byte)2, Z: (byte)2, YSection: (byte)0, BlockId: 0u),
        });

        // Patch the record-count field (lower 24 bits of u32[2]) to 3.
        BinaryPrimitives.WriteUInt32LittleEndian(truthful.AsSpan(8, 4), 0xC0000003u);
        Assert.Throws<InvalidDataException>(() =>
            WorldChunkReader.ReadFromBytes(truthful, chunkId: 0));
    }

    [Theory]
    [InlineData("93_300000004.vw3", 0x300000004UL)]
    [InlineData("93_3000009cf.vw3", 0x3000009cfUL)]
    [InlineData("/some/path/93_30000005b.vw3", 0x30000005bUL)]
    public void ParseChunkIdFromFileName_DecodesHex(string path, ulong expected)
        => Assert.Equal(expected, WorldChunkReader.ParseChunkIdFromFileName(path));

    [Theory]
    [InlineData(0x4UL,  0, 0, 1)]    // bit 2  → Z bit 0
    [InlineData(0x5UL,  1, 0, 1)]    // bits 0,2 → X0, Z0
    [InlineData(0x6UL,  0, 1, 1)]    // bits 1,2 → Y0, Z0
    [InlineData(0x5bUL, 7, 3, 0)]
    [InlineData(0x5dUL, 7, 2, 1)]
    public void ChunkPositionDecoder_DemortonsLow32Bits(ulong chunkIdLow, int x, int y, int z)
    {
        var coord = ChunkPositionDecoder.Decode(chunkIdLow);
        Assert.Equal(new ChunkPositionDecoder.ChunkCoord(x, y, z), coord);
    }

    [Fact]
    public void ChunkPositionDecoder_HighBitsOfIdAreIgnored()
    {
        // The 0x300000000 prefix is a world/planet id we currently ignore.
        var withPrefix = ChunkPositionDecoder.Decode(0x300000004UL);
        var noPrefix = ChunkPositionDecoder.Decode(0x4UL);
        Assert.Equal(noPrefix, withPrefix);
    }

    private static byte[] BuildChunk((byte X, byte YOffset, byte Z, byte YSection, uint BlockId)[] voxels)
    {
        // header (20 bytes) + zstd-compressed body
        using var ms = new MemoryStream();
        Span<byte> hdr = stackalloc byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(0,  4), 0x7Bu);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(4,  4), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(8,  4), 0xC0000000u | (uint)voxels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(12, 4), 0x2u);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(16, 4), 0u);
        ms.Write(hdr);

        var body = new byte[voxels.Length * 8];
        for (int i = 0; i < voxels.Length; i++)
        {
            var v = voxels[i];
            int o = i * 8;
            body[o + 0] = v.X;
            body[o + 1] = v.YOffset;
            body[o + 2] = v.Z;
            body[o + 3] = v.YSection;
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(o + 4, 4), v.BlockId);
        }
        using var compressor = new Compressor();
        var compressed = compressor.Wrap(body).ToArray();
        ms.Write(compressed);

        return ms.ToArray();
    }
}
