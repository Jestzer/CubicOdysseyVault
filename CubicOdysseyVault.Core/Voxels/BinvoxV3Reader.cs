using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace CubicOdysseyVault.Core.Voxels;

// Parses Cubic Odyssey ship_*.vx files. The format ("#binvox 3") is a
// CO-specific extension of Patrick Min's binvox: 5 newline-terminated
// ASCII lines (#binvox 3, dim N N N, translate, scale, data), followed
// by a body of 5-byte RLE records: (uint32 LE block_id, uint8 count).
// Sum of all counts equals dim³, in standard binvox iteration order
// (x outermost, z mid, y innermost; y is vertical).
//
// block_id == 0 is empty/air. Solid voxels are kept sparsely.
public static class BinvoxV3Reader
{
    public static VoxelGrid Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static VoxelGrid Read(Stream stream)
    {
        var (dim, body) = ReadHeaderAndBody(stream);

        if (body.Length % 5 != 0)
            throw new InvalidDataException(
                $"binvox 3 RLE body length {body.Length} is not a multiple of 5 bytes.");

        var voxels = new List<Voxel>();
        long index = 0;
        long total = (long)dim * dim * dim;

        for (int p = 0; p < body.Length; p += 5)
        {
            uint blockId = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(p, 4));
            int count = body[p + 4];

            if (count == 0) continue;

            if (index + count > total)
                throw new InvalidDataException(
                    $"binvox 3 RLE body decodes to more than dim³={total} voxels at record offset {p}.");

            if (blockId != 0)
            {
                // Iterate the run, mapping linear index -> (x, y, z) using
                // standard binvox order: index = x*dim*dim + z*dim + y.
                long end = index + count;
                long i = index;
                while (i < end)
                {
                    int x = (int)(i / ((long)dim * dim));
                    int rem = (int)(i % ((long)dim * dim));
                    int z = rem / dim;
                    int y = rem % dim;
                    voxels.Add(new Voxel(x, y, z, blockId));
                    i++;
                }
            }
            index += count;
        }

        if (index != total)
            throw new InvalidDataException(
                $"binvox 3 RLE body decoded {index} voxels, expected {total}.");

        return new VoxelGrid(dim, voxels);
    }

    private static (int dim, byte[] body) ReadHeaderAndBody(Stream stream)
    {
        // The header is at most 5 short ASCII lines, so a small scratch
        // buffer is plenty. We read the file in one shot to avoid juggling
        // line-by-line stream reading and partial reads.
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();

        // Find the 5th newline.
        int nlCount = 0;
        int headerEnd = -1;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x0A)
            {
                nlCount++;
                if (nlCount == 5)
                {
                    headerEnd = i + 1;
                    break;
                }
            }
        }
        if (headerEnd < 0)
            throw new InvalidDataException("binvox 3 header is malformed: fewer than 5 lines.");

        // Parse the header text. We only require lines 1 (#binvox 3), 2 (dim),
        // and 5 (data). Lines 3 and 4 (translate, scale) are accepted but
        // not consumed — voxels are returned in grid-local coordinates.
        var header = Encoding.ASCII.GetString(data, 0, headerEnd);
        var lines = header.Split('\n', StringSplitOptions.None);
        if (lines.Length < 5)
            throw new InvalidDataException("binvox 3 header is malformed: fewer than 5 lines.");
        if (!lines[0].StartsWith("#binvox", StringComparison.Ordinal))
            throw new InvalidDataException($"binvox 3: first line is not '#binvox 3' (got '{lines[0]}').");
        if (!lines[4].Equals("data", StringComparison.Ordinal))
            throw new InvalidDataException($"binvox 3: fifth line is not 'data' (got '{lines[4]}').");

        var dimParts = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (dimParts.Length != 4 || dimParts[0] != "dim")
            throw new InvalidDataException($"binvox 3: 'dim' line is malformed (got '{lines[1]}').");
        if (!int.TryParse(dimParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dx) ||
            !int.TryParse(dimParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dy) ||
            !int.TryParse(dimParts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dz))
            throw new InvalidDataException($"binvox 3: 'dim' values are not integers (got '{lines[1]}').");
        if (dx != dy || dy != dz)
            throw new InvalidDataException($"binvox 3: non-cubic grid {dx}x{dy}x{dz} is not supported.");
        if (dx <= 0 || dx > 1024)
            throw new InvalidDataException($"binvox 3: implausible dim {dx} (expected 1..1024).");

        var body = new byte[data.Length - headerEnd];
        Buffer.BlockCopy(data, headerEnd, body, 0, body.Length);
        return (dx, body);
    }
}
