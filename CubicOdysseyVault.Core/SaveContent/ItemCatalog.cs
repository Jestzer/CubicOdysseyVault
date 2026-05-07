namespace CubicOdysseyVault.Core.SaveContent;

public sealed class ItemCatalog
{
    public IReadOnlyDictionary<string, ItemMetadata> ByIdentifier { get; }
    public bool IsEmpty => ByIdentifier.Count == 0;

    public ItemCatalog(IReadOnlyDictionary<string, ItemMetadata> byIdentifier)
    {
        ByIdentifier = byIdentifier;
    }

    public ItemMetadata? Lookup(string identifier) =>
        ByIdentifier.TryGetValue(identifier, out var m) ? m : null;

    public static ItemCatalog Empty { get; } =
        new(new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase));

    // Walks <gameInstall>/data/configs/items/*.cfg and parses each file.
    // Errors on individual files are swallowed — we return whatever parsed.
    public static ItemCatalog LoadFrom(string gameInstallDir)
    {
        if (string.IsNullOrEmpty(gameInstallDir)) return Empty;
        var configDir = Path.Combine(gameInstallDir, "data", "configs", "items");
        if (!Directory.Exists(configDir)) return Empty;

        var dict = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(configDir, "*.cfg"))
        {
            var meta = ItemConfigParser.ParseFile(path);
            if (meta == null) continue;
            dict[meta.Identifier] = meta;
        }
        return new ItemCatalog(dict);
    }

    // Tries common Cubic Odyssey install locations across Steam libraries.
    // Returns the first one that yields a non-empty catalog.
    public static ItemCatalog AutoDiscover(IEnumerable<string> candidateInstallDirs)
    {
        foreach (var dir in candidateInstallDirs)
        {
            var catalog = LoadFrom(dir);
            if (!catalog.IsEmpty) return catalog;
        }
        return Empty;
    }

    // Humanizes an identifier for display when no localized title is available.
    // "cloth.suit.2" → "Suit (Tier 2)"
    // "wep.mining_laser.3" → "Mining Laser (Tier 3)"
    // "comp.karve.engine.space.mk1" → "Karve Engine Space Mk1"
    public static string HumanizeIdentifier(string identifier, int tier = 0)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;
        var parts = identifier.Split('.');
        // Drop the category prefix (cloth/wep/res/comp/dpl) for display
        int start = parts[0] is "cloth" or "wep" or "res" or "comp" or "dpl" ? 1 : 0;

        // If the trailing token is a small integer treat as tier and drop;
        // surface it as "(Tier N)" instead.
        int last = parts.Length - 1;
        int extractedTier = 0;
        if (last > start && int.TryParse(parts[last], out extractedTier) && extractedTier <= 99)
            last--;

        var nameParts = new List<string>();
        for (int i = start; i <= last; i++)
        {
            var token = parts[i].Replace('_', ' ');
            if (token.Length == 0) continue;
            // Title-case each word.
            var words = token.Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                var word = words[w];
                if (word.Length == 0) continue;
                words[w] = char.ToUpperInvariant(word[0]) + word[1..];
            }
            nameParts.Add(string.Join(' ', words));
        }
        var name = string.Join(' ', nameParts);
        var resolvedTier = tier > 0 ? tier : extractedTier;
        return resolvedTier > 0 ? $"{name} (Tier {resolvedTier})" : name;
    }
}
