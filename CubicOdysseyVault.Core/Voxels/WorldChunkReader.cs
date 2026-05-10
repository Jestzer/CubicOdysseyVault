using System.Buffers.Binary;
using System.Globalization;
using ZstdSharp;

namespace CubicOdysseyVault.Core.Voxels;

// Parses Cubic Odyssey 93_*.vw3 files. Format reverse-engineered against
// real saves (see /docs/HANDOFF.md and the project's exploratory notes):
//
//   [0..20)   20-byte header:
//             u32[0..2]   8-byte hash (low→high). Pristine chunks have small
//                         id-like values (e.g. 0x7B 0x0); modified chunks
//                         have a real hash.
//             u32[2]      lower 24 bits = record count; high byte = flags.
//                         Observed flags only ever 0xC0.
//             u32[3]      version; observed = 0x2 only.
//             u32[4]      uninterpreted (not record count, not decompressed
//                         size). Captured for forward-compat.
//
//   [20..)    zstd frame (magic 28 B5 2F FD) → N × 8-byte records, where
//             N == header.RecordCount.
//
// Each 8-byte record:
//
//   bytes 0..4  packed voxel position. Byte layout chosen empirically:
//                    byte 0      X            (0..255, dense iteration axis)
//                    byte 1      Y offset     (0..31 observed — 5 bits)
//                    byte 2      Z            (0..255)
//                    byte 3      Y section    (0..30 observed — 5 bits)
//               Voxel Y is reconstructed as `byte3 * 32 + byte1`, giving a
//               chunk-local 10-bit Y with range 0..1023. The section / offset
//               split (vs. a single byte for Y) was confirmed by checking
//               that byte 1 never exceeds 31 within any byte 3 plateau.
//
//   bytes 4..8  block info. The full uint32 is used as the palette key
//               (high byte appears to be the type; lower 3 bytes are
//               variant/flags but we don't need to parse them yet).
//
// Voxel positions are CHUNK-LOCAL; combining chunks needs ChunkPositionDecoder.
public static class WorldChunkReader
{
    public static WorldChunk Read(string path)
    {
        var data = File.ReadAllBytes(path);
        var chunkId = ParseChunkIdFromFileName(path);
        return ReadFromBytes(data, chunkId);
    }

    public static WorldChunk ReadFromBytes(byte[] data, ulong chunkId)
    {
        if (data.Length < 24)
            throw new InvalidDataException($"vw3 file too short ({data.Length} bytes); needs at least 20-byte header + zstd frame.");

        var hdrSpan = data.AsSpan(0, 20);
        var u0 = BinaryPrimitives.ReadUInt32LittleEndian(hdrSpan[..4]);
        var u1 = BinaryPrimitives.ReadUInt32LittleEndian(hdrSpan[4..8]);
        var u2 = BinaryPrimitives.ReadUInt32LittleEndian(hdrSpan[8..12]);
        var u3 = BinaryPrimitives.ReadUInt32LittleEndian(hdrSpan[12..16]);
        var u4 = BinaryPrimitives.ReadUInt32LittleEndian(hdrSpan[16..20]);

        var header = new WorldChunkHeader(
            Hash: ((ulong)u1 << 32) | u0,
            RecordCount: (int)(u2 & 0x00FFFFFFu),
            Flags: (byte)(u2 >> 24),
            Version: u3,
            Trailer: u4);

        // Body must start with a zstd magic at byte 20.
        var bodyMagic = data.AsSpan(20, 4);
        if (bodyMagic[0] != 0x28 || bodyMagic[1] != 0xB5 || bodyMagic[2] != 0x2F || bodyMagic[3] != 0xFD)
            throw new InvalidDataException($"vw3: byte 20 is not a zstd frame ({bodyMagic[0]:x2} {bodyMagic[1]:x2} {bodyMagic[2]:x2} {bodyMagic[3]:x2}).");

        // ZstdSharp's `Decompressor.Unwrap` requires the frame to carry a
        // decompressed-size header; vw3 zstd frames don't, so use the
        // streaming variant. We size the output buffer from the expected
        // record count and use a cancellation-aware copy with a hard cap to
        // protect against pathological inputs.
        var expected = header.RecordCount * 8;
        var decompressed = new byte[expected];
        using (var input = new MemoryStream(data, 20, data.Length - 20, writable: false))
        using (var stream = new DecompressionStream(input))
        {
            int total = 0;
            while (total < expected)
            {
                int n = stream.Read(decompressed, total, expected - total);
                if (n == 0) break;
                total += n;
            }
            if (total != expected)
                throw new InvalidDataException(
                    $"vw3 body decoded {total} bytes, expected {expected} ({header.RecordCount} records × 8 bytes).");
        }

        var voxels = new List<Voxel>(header.RecordCount);
        for (int i = 0; i < header.RecordCount; i++)
        {
            int o = i * 8;
            byte x = decompressed[o + 0];
            byte yOffset = decompressed[o + 1];
            byte z = decompressed[o + 2];
            byte ySection = decompressed[o + 3];
            uint blockId = BinaryPrimitives.ReadUInt32LittleEndian(decompressed.AsSpan(o + 4, 4));

            int y = (ySection * 32) + yOffset;
            voxels.Add(new Voxel(x, y, z, blockId));
        }

        return new WorldChunk(chunkId, header, voxels);
    }

    // Filenames look like "93_3000009cf.vw3"; the hex digits between "93_"
    // and ".vw3" are the chunk id. We accept upper/lower-case hex.
    public static ulong ParseChunkIdFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith("93_", StringComparison.Ordinal))
            throw new ArgumentException($"vw3 filename '{name}' does not start with '93_'.");
        var hex = name.Substring(3);
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
            throw new ArgumentException($"vw3 filename '{name}' has non-hex chunk id '{hex}'.");
        return id;
    }
}

// Maps a vw3 chunk id (e.g. 0x300000004) to its world-space origin.
//
// Empirically the low 32 bits of the chunk id are a 3D Morton code (bit-
// interleaved coordinates). Chunks 0x4 / 0x5 / 0x6 demorton to (0,0,1) /
// (1,0,1) / (0,1,1) — adjacent in space — which is the consistency check
// that nailed down the bit ordering (X = bits 0,3,6,…; Y = bits 1,4,7,…;
// Z = bits 2,5,8,…).
//
// The high 32 bits (always 0x3 in observed saves) are treated as a world
// /planet identifier and ignored for now.
public static class ChunkPositionDecoder
{
    // Chunk-local X and Z each fit in one byte (0..255). Chunk-local Y is the
    // 10-bit (section*32 + offset) value the reader unpacks, range 0..1023.
    // The Morton X/Z coords place chunks horizontally; Morton Y is a coarse
    // altitude band — stride matches the within-chunk Y extent so chunks at
    // different Morton Y stack without overlap and without huge empty gaps.
    public const int ChunkSpanXZ = 256;
    public const int ChunkSpanY = 1024;

    public readonly record struct ChunkCoord(int X, int Y, int Z);

    public static ChunkCoord Decode(ulong chunkId)
    {
        // Use a 64-bit value for the shifts so we can safely walk past bit 31
        // without C#'s `uint >> 32 == self` quirk; bits above 31 of the low
        // 32 are zero anyway.
        ulong bits = chunkId & 0xFFFFFFFFUL;
        int x = 0, y = 0, z = 0;
        // 32 bits / 3 axes → 11 axis-bits max, but we only iterate while there
        // are still source bits left to consume.
        for (int i = 0; i < 11; i++)
        {
            int xBit = (i * 3 + 0);
            int yBit = (i * 3 + 1);
            int zBit = (i * 3 + 2);
            if (xBit < 32) x |= (int)((bits >> xBit) & 1UL) << i;
            if (yBit < 32) y |= (int)((bits >> yBit) & 1UL) << i;
            if (zBit < 32) z |= (int)((bits >> zBit) & 1UL) << i;
        }
        return new ChunkCoord(x, y, z);
    }

    // Convert a chunk-local voxel into world-space integer coords.
    public static (int X, int Y, int Z) ToWorld(ChunkCoord chunk, Voxel local) =>
        (chunk.X * ChunkSpanXZ + local.X,
         chunk.Y * ChunkSpanY  + local.Y,
         chunk.Z * ChunkSpanXZ + local.Z);
}
