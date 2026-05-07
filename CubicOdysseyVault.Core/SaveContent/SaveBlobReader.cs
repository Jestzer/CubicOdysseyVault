using ZstdSharp;

namespace CubicOdysseyVault.Core.SaveContent;

// Cubic Odyssey *.sav files are [u32 little-endian length prefix][zstd frame].
// The prefix is the decompressed payload size. We strip it, decompress with
// ZstdSharp.Port, and hand both raw + decompressed bytes back to the UI so
// the inspector can show either.
public static class SaveBlobReader
{
    private static readonly byte[] ZstdMagic = { 0x28, 0xB5, 0x2F, 0xFD };

    public static SaveBlob ReadFile(string path) => ReadBytes(File.ReadAllBytes(path));

    public static SaveBlob ReadBytes(byte[] raw)
    {
        if (raw.Length < 8 || !HasZstdMagicAt(raw, 4))
            return new SaveBlob(raw, null, "Not a Cubic Odyssey save blob (missing u32 prefix + zstd magic).");

        int expectedDecompressedSize = raw[0] | (raw[1] << 8) | (raw[2] << 16) | (raw[3] << 24);

        try
        {
            using var decompressor = new Decompressor();
            var compressed = new ReadOnlySpan<byte>(raw, 4, raw.Length - 4);
            var decompressed = decompressor.Unwrap(compressed).ToArray();
            if (expectedDecompressedSize > 0 && decompressed.Length != expectedDecompressedSize)
            {
                return new SaveBlob(
                    raw, decompressed,
                    $"Decompressed size {decompressed.Length} does not match prefix {expectedDecompressedSize}.");
            }
            return new SaveBlob(raw, decompressed, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new SaveBlob(raw, null, $"Zstd decompression failed: {ex.Message}");
        }
    }

    private static bool HasZstdMagicAt(byte[] data, int offset)
    {
        if (data.Length - offset < ZstdMagic.Length) return false;
        for (int i = 0; i < ZstdMagic.Length; i++)
            if (data[offset + i] != ZstdMagic[i]) return false;
        return true;
    }
}
