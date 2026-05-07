using CubicOdysseyVault.Core.SaveContent;
using Xunit;
using ZstdSharp;

namespace CubicOdysseyVault.Tests;

public class SaveBlobReaderTests
{
    [Fact]
    public void ReadBytes_ValidPrefixAndZstd_DecompressesPayload()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var raw = MakeSavePayload(payload);

        var blob = SaveBlobReader.ReadBytes(raw);

        Assert.Null(blob.ErrorMessage);
        Assert.NotNull(blob.DecompressedBytes);
        Assert.Equal(payload, blob.DecompressedBytes);
    }

    [Fact]
    public void ReadBytes_NoZstdMagic_ReturnsRawWithError()
    {
        var raw = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // bytes 4-7 aren't the zstd magic
        var blob = SaveBlobReader.ReadBytes(raw);

        Assert.Null(blob.DecompressedBytes);
        Assert.NotNull(blob.ErrorMessage);
        Assert.Equal(raw, blob.RawBytes);
    }

    [Fact]
    public void ReadBytes_TooShort_ReturnsRawWithError()
    {
        var raw = new byte[] { 1, 2 };
        var blob = SaveBlobReader.ReadBytes(raw);
        Assert.Null(blob.DecompressedBytes);
        Assert.NotNull(blob.ErrorMessage);
    }

    [Fact]
    public void ReadBytes_TruncatedZstdFrame_ReturnsRawWithError()
    {
        var raw = MakeSavePayload(new byte[] { 1, 2, 3, 4 });
        // Chop off the tail so the zstd frame is broken.
        var truncated = raw.Take(raw.Length - 3).ToArray();
        var blob = SaveBlobReader.ReadBytes(truncated);
        Assert.NotNull(blob.ErrorMessage);
    }

    private static byte[] MakeSavePayload(byte[] payload)
    {
        using var compressor = new Compressor();
        var compressed = compressor.Wrap(payload).ToArray();
        var raw = new byte[4 + compressed.Length];
        raw[0] = (byte)(payload.Length & 0xFF);
        raw[1] = (byte)((payload.Length >> 8) & 0xFF);
        raw[2] = (byte)((payload.Length >> 16) & 0xFF);
        raw[3] = (byte)((payload.Length >> 24) & 0xFF);
        Buffer.BlockCopy(compressed, 0, raw, 4, compressed.Length);
        return raw;
    }
}
