using System.Text;

namespace CubicOdysseyVault.Core.SaveContent;

public static class HexFormatter
{
    // Formats a buffer as classic xxd-style lines: <8-hex offset>  <hex bytes>  <ASCII pane>
    public static IReadOnlyList<string> Format(byte[] data, int bytesPerLine = 16)
    {
        if (bytesPerLine <= 0) bytesPerLine = 16;
        var lines = new List<string>((data.Length + bytesPerLine - 1) / bytesPerLine);
        var sb = new StringBuilder();
        for (int offset = 0; offset < data.Length; offset += bytesPerLine)
        {
            int n = Math.Min(bytesPerLine, data.Length - offset);
            sb.Clear();
            sb.Append(offset.ToString("x8")).Append("  ");
            for (int i = 0; i < bytesPerLine; i++)
            {
                if (i < n) sb.Append(data[offset + i].ToString("x2")).Append(' ');
                else sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(' ');
            for (int i = 0; i < n; i++)
            {
                byte b = data[offset + i];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            lines.Add(sb.ToString());
        }
        return lines;
    }
}
