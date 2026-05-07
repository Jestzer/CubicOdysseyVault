namespace CubicOdysseyVault.Core.SaveContent;

// Cubic Odyssey ships per-item metadata as plain-text *.cfg files at
//   <install>/data/configs/items/*.cfg
// Each file is a single ItemCfg block, e.g.:
//   ItemCfg
//   {
//       id            14
//       identifier    "cloth.suit.1"
//       type          GEAR
//       tier          1
//       title_string  "STR_SUIT_1"
//       ...
//   }
// We parse the keys we care about and ignore the rest. Quotes and whitespace
// tolerant. Returns null if the file isn't a valid ItemCfg.
public static class ItemConfigParser
{
    public static ItemMetadata? ParseFile(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static ItemMetadata? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!text.Contains("ItemCfg", StringComparison.Ordinal)) return null;

        string? identifier = null;
        string typeRaw = "";
        string titleString = "";
        int tier = 0, invFrame = 0, stackSize = 1, recycle = 0, basePrice = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;

            // Each kv line is "<key><whitespace><value-maybe-quoted>" — and may
            // contain a comment after. We only need the first value token.
            var (key, value) = SplitKeyValue(line);
            if (key.Length == 0) continue;

            switch (key)
            {
                case "identifier": identifier = Unquote(value); break;
                case "type": typeRaw = Unquote(value); break;
                case "tier": int.TryParse(Unquote(value), out tier); break;
                case "title_string": titleString = Unquote(value); break;
                case "inv_frame": int.TryParse(Unquote(value), out invFrame); break;
                case "stack_size": int.TryParse(Unquote(value), out stackSize); break;
                case "recycle_value": int.TryParse(Unquote(value), out recycle); break;
                case "base_price": int.TryParse(Unquote(value), out basePrice); break;
            }
        }

        if (string.IsNullOrEmpty(identifier)) return null;
        return new ItemMetadata(identifier, titleString, typeRaw, tier, invFrame, stackSize, recycle, basePrice);
    }

    private static (string Key, string Value) SplitKeyValue(string line)
    {
        // Skip leading non-identifier chars (covers stray '{' '}')
        int i = 0;
        while (i < line.Length && !IsIdentChar(line[i])) i++;
        int keyStart = i;
        while (i < line.Length && IsIdentChar(line[i])) i++;
        if (i == keyStart) return ("", "");
        var key = line[keyStart..i];

        // Skip whitespace between key and value
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        if (i >= line.Length) return (key, "");

        // Take value: rest of line up to (a) end, (b) "//" comment.
        var rest = line[i..];
        int commentIdx = rest.IndexOf("//", StringComparison.Ordinal);
        if (commentIdx >= 0) rest = rest[..commentIdx];
        return (key, rest.Trim());
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }
}
