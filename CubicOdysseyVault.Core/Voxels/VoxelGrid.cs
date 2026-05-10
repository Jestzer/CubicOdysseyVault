namespace CubicOdysseyVault.Core.Voxels;

// Sparse voxel grid: only solid voxels are kept. Origin is (0,0,0) at one
// corner; Y is the vertical (up) axis to match binvox / typical voxel-tool
// conventions. BlockId is the 32-bit value pulled from the file's RLE
// records — its high two bytes are observed to always be zero in CO ship
// files but we keep the full uint32 for forward-compat / world-chunk reuse.
public readonly record struct Voxel(int X, int Y, int Z, uint BlockId);

public sealed class VoxelGrid
{
    public int Dim { get; }
    public IReadOnlyList<Voxel> SolidVoxels { get; }

    public int SolidCount => SolidVoxels.Count;
    public int DistinctBlockTypes { get; }

    public VoxelGrid(int dim, IReadOnlyList<Voxel> solidVoxels)
    {
        Dim = dim;
        SolidVoxels = solidVoxels;
        var distinct = new HashSet<uint>();
        foreach (var v in solidVoxels) distinct.Add(v.BlockId);
        DistinctBlockTypes = distinct.Count;
    }
}
