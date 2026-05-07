namespace CubicOdysseyVault.Core.SaveContent;

public sealed record ExtractedString(int Offset, string Text);

public static class StringsExtractor
{
    // Like the unix `strings` tool: emit any printable-ASCII run of at least
    // `minLength` bytes terminated by a non-printable byte. UTF-8 multi-byte
    // sequences aren't decoded here — every save file we've seen is ASCII for
    // the human-readable bits.
    public static IReadOnlyList<ExtractedString> Extract(byte[] data, int minLength = 4)
    {
        if (minLength < 1) minLength = 1;
        var results = new List<ExtractedString>();
        int runStart = -1;
        for (int i = 0; i <= data.Length; i++)
        {
            bool isEnd = i == data.Length;
            bool printable = !isEnd && IsPrintable(data[i]);
            if (printable)
            {
                if (runStart < 0) runStart = i;
            }
            else if (runStart >= 0)
            {
                int runLen = i - runStart;
                if (runLen >= minLength)
                {
                    var text = System.Text.Encoding.ASCII.GetString(data, runStart, runLen);
                    results.Add(new ExtractedString(runStart, text));
                }
                runStart = -1;
            }
        }
        return results;
    }

    private static bool IsPrintable(byte b) => b >= 0x20 && b < 0x7F;
}
