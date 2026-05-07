namespace CubicOdysseyVault.Core.SaveContent;

public enum TlvValueKind
{
    Byte,
    Int32,
    UInt32,
    Int64,
    Float32,
    Float64,
    List,
    Unknown,
}

public sealed record TlvEntry(
    int Tag,
    int RawType,
    TlvValueKind Kind,
    byte[] RawData,
    IReadOnlyList<TlvEntry>? Nested);

public sealed record TlvDocument(
    int HeaderTag,
    IReadOnlyList<TlvEntry> Entries,
    string? ParseError);

// Parses Cubic Odyssey's decompressed save format:
//
//   header: u32 (always 0x00000008 in saves seen so far)
//   count:  u16
//   entries: <count> times of:
//     tag:    u16
//     type:   u16
//     length: u32
//     data:   <length> bytes
//
// Type 23 (0x17) wraps a nested document of the same shape (count + entries).
// Other recognized type codes map to fixed-width integers / floats; unknown
// types pass through with Kind=Unknown and the raw bytes still surfaced.
public static class TlvParser
{
    public static TlvDocument Parse(byte[] decompressed)
    {
        if (decompressed.Length < 6)
            return new TlvDocument(0, Array.Empty<TlvEntry>(), "Buffer too short for header.");

        int offset = 0;
        int headerTag = ReadInt32(decompressed, ref offset);

        var entries = new List<TlvEntry>();
        var error = ParseEntries(decompressed, ref offset, entries);
        return new TlvDocument(headerTag, entries, error);
    }

    private static string? ParseEntries(byte[] data, ref int offset, List<TlvEntry> entries)
    {
        if (offset + 2 > data.Length)
            return "Unexpected end of data while reading entry count.";

        int count = ReadUInt16(data, ref offset);

        for (int i = 0; i < count; i++)
        {
            if (offset + 8 > data.Length)
                return $"Unexpected end of data while reading entry {i + 1}/{count} header.";

            int tag = ReadUInt16(data, ref offset);
            int rawType = ReadUInt16(data, ref offset);
            int length = ReadInt32(data, ref offset);

            if (length < 0 || offset + length > data.Length)
                return $"Entry {i + 1}/{count} (tag {tag}, type {rawType}) declares length {length} that exceeds buffer.";

            var rawData = new byte[length];
            Buffer.BlockCopy(data, offset, rawData, 0, length);

            var kind = ClassifyType(rawType, length);
            IReadOnlyList<TlvEntry>? nested = null;
            if (kind == TlvValueKind.List)
            {
                int nestedOffset = 0;
                var nestedEntries = new List<TlvEntry>();
                ParseEntries(rawData, ref nestedOffset, nestedEntries);
                nested = nestedEntries;
            }

            entries.Add(new TlvEntry(tag, rawType, kind, rawData, nested));
            offset += length;
        }

        return null;
    }

    private static TlvValueKind ClassifyType(int rawType, int length) => rawType switch
    {
        1 when length == 1 => TlvValueKind.Byte,
        4 when length == 4 => TlvValueKind.Int32,
        8 when length == 4 => TlvValueKind.UInt32,
        9 when length == 8 => TlvValueKind.Int64,
        10 when length == 4 => TlvValueKind.Float32,
        11 when length == 8 => TlvValueKind.Float64,
        23 => TlvValueKind.List,
        _ => TlvValueKind.Unknown,
    };

    private static int ReadUInt16(byte[] data, ref int offset)
    {
        int v = data[offset] | (data[offset + 1] << 8);
        offset += 2;
        return v;
    }

    private static int ReadInt32(byte[] data, ref int offset)
    {
        int v = data[offset]
              | (data[offset + 1] << 8)
              | (data[offset + 2] << 16)
              | (data[offset + 3] << 24);
        offset += 4;
        return v;
    }
}
