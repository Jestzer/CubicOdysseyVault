using System.Buffers.Binary;
using System.Text;
using CubicOdysseyVault.Core.Voxels;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class BinvoxV3ReaderTests
{
    [Fact]
    public void Read_AllAir_ReturnsEmptyGrid()
    {
        // 4x4x4 grid, all air (block_id = 0). One run of 64 zeros.
        var bytes = BuildFile(dim: 4, body: BuildRle((0u, 64)));
        var grid = BinvoxV3Reader.Read(new MemoryStream(bytes));

        Assert.Equal(4, grid.Dim);
        Assert.Empty(grid.SolidVoxels);
        Assert.Equal(0, grid.DistinctBlockTypes);
    }

    [Fact]
    public void Read_SingleSolidAtIndex0_PlacesVoxelAtOrigin()
    {
        // 4³ grid: 1 solid voxel of block 0xCB02 then 63 air.
        var bytes = BuildFile(dim: 4, body: BuildRle((0xCB02u, 1), (0u, 63)));
        var grid = BinvoxV3Reader.Read(new MemoryStream(bytes));

        var only = Assert.Single(grid.SolidVoxels);
        Assert.Equal(new Voxel(X: 0, Y: 0, Z: 0, BlockId: 0xCB02u), only);
    }

    [Fact]
    public void Read_OrderingMatchesStandardBinvox()
    {
        // Index ordering: i = x*dim*dim + z*dim + y. For dim=4, putting a
        // single solid at i=1 should be (x=0, z=0, y=1). At i=4 → (0,1,0).
        // At i=16 → (1,0,0).
        var bytes = BuildFile(dim: 4,
            body: BuildRle(
                (0u, 1),       // i=0  -> air
                (0xAAu, 1),    // i=1  -> (0,1,0)
                (0u, 2),       // i=2,3
                (0xBBu, 1),    // i=4  -> (0,0,1)
                (0u, 11),      // i=5..15
                (0xCCu, 1),    // i=16 -> (1,0,0)
                (0u, 47)));    // i=17..63
        var grid = BinvoxV3Reader.Read(new MemoryStream(bytes));

        Assert.Collection(grid.SolidVoxels,
            v => Assert.Equal(new Voxel(0, 1, 0, 0xAAu), v),
            v => Assert.Equal(new Voxel(0, 0, 1, 0xBBu), v),
            v => Assert.Equal(new Voxel(1, 0, 0, 0xCCu), v));
        Assert.Equal(3, grid.DistinctBlockTypes);
    }

    [Fact]
    public void Read_RunLongerThanOne_ExpandsToConsecutiveVoxels()
    {
        // dim=4, 4 voxels of block 0x42 then 60 air. Y is the inner axis,
        // so a run of 4 starting at i=0 fills (0,0,0)..(0,3,0) — a column
        // along the Y (vertical) axis.
        var bytes = BuildFile(dim: 4, body: BuildRle((0x42u, 4), (0u, 60)));
        var grid = BinvoxV3Reader.Read(new MemoryStream(bytes));

        Assert.Equal(4, grid.SolidCount);
        for (int y = 0; y < 4; y++)
            Assert.Contains(new Voxel(0, y, 0, 0x42u), grid.SolidVoxels);
    }

    [Fact]
    public void Read_InvalidHeaderMissingLines_Throws()
    {
        var bytes = Encoding.ASCII.GetBytes("#binvox 3\ndim 4 4 4\n");
        Assert.Throws<InvalidDataException>(() => BinvoxV3Reader.Read(new MemoryStream(bytes)));
    }

    [Fact]
    public void Read_NonCubicDim_Throws()
    {
        var hdr = "#binvox 3\ndim 4 8 4\ntranslate 0 0 0\nscale 1\ndata\n";
        var body = BuildRle((0u, 4 * 8 * 4));
        var bytes = Encoding.ASCII.GetBytes(hdr).Concat(body).ToArray();
        Assert.Throws<InvalidDataException>(() => BinvoxV3Reader.Read(new MemoryStream(bytes)));
    }

    [Fact]
    public void Read_MisalignedBody_Throws()
    {
        // 4³ grid with a body that's 1 byte short of a record boundary.
        var hdr = Encoding.ASCII.GetBytes("#binvox 3\ndim 4 4 4\ntranslate 0 0 0\nscale 1\ndata\n");
        var body = BuildRle((0u, 64));
        var trimmed = body.AsSpan(0, body.Length - 1).ToArray();
        var bytes = hdr.Concat(trimmed).ToArray();
        Assert.Throws<InvalidDataException>(() => BinvoxV3Reader.Read(new MemoryStream(bytes)));
    }

    [Fact]
    public void Read_DecodedVoxelCountExceedsDimCubed_Throws()
    {
        // dim=2 (8 voxels), but body claims 9 voxels.
        var hdr = Encoding.ASCII.GetBytes("#binvox 3\ndim 2 2 2\ntranslate 0 0 0\nscale 1\ndata\n");
        var body = BuildRle((0u, 9));
        Assert.Throws<InvalidDataException>(() => BinvoxV3Reader.Read(new MemoryStream(hdr.Concat(body).ToArray())));
    }

    private static byte[] BuildFile(int dim, byte[] body)
    {
        var hdr = Encoding.ASCII.GetBytes(
            $"#binvox 3\ndim {dim} {dim} {dim}\ntranslate 0 0 0\nscale 1\ndata\n");
        return hdr.Concat(body).ToArray();
    }

    private static byte[] BuildRle(params (uint blockId, byte count)[] runs)
    {
        var ms = new MemoryStream();
        Span<byte> rec = stackalloc byte[5];
        foreach (var (blockId, count) in runs)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rec[..4], blockId);
            rec[4] = count;
            ms.Write(rec);
        }
        return ms.ToArray();
    }
}
