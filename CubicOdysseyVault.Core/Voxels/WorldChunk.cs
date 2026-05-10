namespace CubicOdysseyVault.Core.Voxels;

// Parsed Cubic Odyssey *.vw3 world chunk. Voxel positions are CHUNK-LOCAL
// (each chunk has its own coordinate frame); use ChunkPositionDecoder to
// turn ChunkId into a world-space origin for combining chunks.
//
// Header is 5 little-endian uint32s in the file; we parse them with names
// that reflect what's actually known. u32[4] is currently uninterpreted.
public readonly record struct WorldChunkHeader(
    ulong Hash,           // u32[0] | (u32[1] << 32) — 0x7B / 0 for pristine chunks
    int RecordCount,      // (u32[2] & 0x00FFFFFF)
    byte Flags,           // u32[2] >> 24 — observed 0xC0 only, kept for forward-compat
    uint Version,         // u32[3] — 0x2 in all observed chunks
    uint Trailer);        // u32[4] — purpose unknown (not record count, not size)

public sealed record WorldChunk(
    ulong ChunkId,
    WorldChunkHeader Header,
    IReadOnlyList<Voxel> Voxels);
