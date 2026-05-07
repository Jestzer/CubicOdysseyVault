using System.Text;
using System.Text.RegularExpressions;

namespace CubicOdysseyVault.Core.SaveContent;

// Pulls inventory items out of decompressed 93_client_state.sav bytes.
//
// We don't fully decode the TLV structure for inventory containers (the game's
// container types vary in shape) — instead we scan for a pattern that's
// stable across every item record we've seen:
//
//   <inventory item TLV record>:
//     tag=1 type=12 (string)  length=L  data=<u16 char_count><identifier bytes><\0>
//     tag=2 type=10 (float32) length=4  data=<durability>
//     tag=3 type=4  (int32)   length=4  data=<count>
//     ...remaining tags vary per item...
//
// So for each identifier string in the buffer (matched by category prefix),
// we look immediately after the null terminator for the literal byte pattern
//   02 00 0a 00 04 00 00 00 ?? ?? ?? ?? 03 00 04 00 04 00 00 00 <int32 count>
// and pull the int32. If the pattern doesn't match (some items have a
// different shape) we fall back to count=1.
//
// Inventory section grouping uses the literal "inventory" / "__quickslots"
// strings as fence posts — items before the first marker are treated as
// "Equipped", items between markers go to that section, and any items after
// the last "inventory" marker (typically ship cargo) go to "Ship inventory".
public static class InventoryExtractor
{
    private static readonly Regex IdentifierPattern = new(
        @"^(cloth|wep|res|comp|dpl)\.[a-z0-9_.\-]+$",
        RegexOptions.Compiled);

    private static readonly byte[] CountSignature =
    {
        // tag=2 type=10 (float32) length=4
        0x02, 0x00, 0x0A, 0x00, 0x04, 0x00, 0x00, 0x00,
        // 4 bytes durability (any value)
        0x00, 0x00, 0x00, 0x00,  // <- masked when matching
        // tag=3 type=4 (int32) length=4
        0x03, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00,
        // 4 bytes count (read out)
    };

    public static IReadOnlyList<InventoryContainer> Extract(byte[] decompressedClientState, ItemCatalog catalog)
    {
        if (decompressedClientState == null || decompressedClientState.Length == 0)
            return Array.Empty<InventoryContainer>();

        var data = decompressedClientState;
        var rawHits = ScanItemHits(data);
        var sections = FindSectionMarkers(data);

        var byContainer = new Dictionary<string, List<InventoryItem>>(StringComparer.Ordinal);
        foreach (var hit in rawHits)
        {
            var meta = catalog.Lookup(hit.Identifier);
            var container = AssignContainer(hit.IdentifierStart, sections);
            if (!byContainer.TryGetValue(container, out var list))
                byContainer[container] = list = new List<InventoryItem>();
            list.Add(new InventoryItem(hit.Identifier, hit.Count, meta));
        }

        // Stable display order: equipped → quickslots → inventory → ship inventory → other
        var order = new[] { "equipped", "__quickslots", "inventory", "ship_inventory" };
        return byContainer
            .Select(kv => new InventoryContainer(kv.Key, FriendlyContainerName(kv.Key), kv.Value))
            .OrderBy(c => Array.IndexOf(order, c.Name) is var idx && idx < 0 ? int.MaxValue : idx)
            .ToList();
    }

    private static string FriendlyContainerName(string raw) => raw switch
    {
        "equipped" => "Equipped",
        "__quickslots" => "Quickslots",
        "inventory" => "Inventory",
        "ship_inventory" => "Ship cargo",
        _ => raw,
    };

    private record ItemHit(string Identifier, int IdentifierStart, int Count);

    // Walks every printable-ASCII run in the buffer; emits a hit for each
    // run whose text matches our item-id pattern.
    private static List<ItemHit> ScanItemHits(byte[] data)
    {
        var hits = new List<ItemHit>();
        int i = 0;
        while (i < data.Length)
        {
            if (!IsPrintable(data[i])) { i++; continue; }
            int start = i;
            while (i < data.Length && IsPrintable(data[i])) i++;
            int len = i - start;
            if (len < 5) continue; // too short to be an item id
            var text = Encoding.ASCII.GetString(data, start, len);
            if (!IdentifierPattern.IsMatch(text)) continue;
            int afterNull = i; // i now points at first non-printable; usually \0
            if (afterNull < data.Length && data[afterNull] == 0) afterNull++;
            int count = TryReadCount(data, afterNull);
            hits.Add(new ItemHit(text, start, count));
        }
        return hits;
    }

    private static int TryReadCount(byte[] data, int afterIdentifierEnd)
    {
        // Need 24 bytes after the identifier+null to fit the signature + int32.
        if (afterIdentifierEnd + 24 > data.Length) return 1;

        // Compare tag/type/length sigil bytes (positions 0-7 and 12-19)
        if (data[afterIdentifierEnd + 0] != 0x02 || data[afterIdentifierEnd + 1] != 0x00) return 1;
        if (data[afterIdentifierEnd + 2] != 0x0A || data[afterIdentifierEnd + 3] != 0x00) return 1;
        if (data[afterIdentifierEnd + 4] != 0x04 || data[afterIdentifierEnd + 5] != 0x00) return 1;
        if (data[afterIdentifierEnd + 6] != 0x00 || data[afterIdentifierEnd + 7] != 0x00) return 1;

        if (data[afterIdentifierEnd + 12] != 0x03 || data[afterIdentifierEnd + 13] != 0x00) return 1;
        if (data[afterIdentifierEnd + 14] != 0x04 || data[afterIdentifierEnd + 15] != 0x00) return 1;
        if (data[afterIdentifierEnd + 16] != 0x04 || data[afterIdentifierEnd + 17] != 0x00) return 1;
        if (data[afterIdentifierEnd + 18] != 0x00 || data[afterIdentifierEnd + 19] != 0x00) return 1;

        int count = data[afterIdentifierEnd + 20]
                  | (data[afterIdentifierEnd + 21] << 8)
                  | (data[afterIdentifierEnd + 22] << 16)
                  | (data[afterIdentifierEnd + 23] << 24);
        if (count <= 0 || count > 100_000) return 1;
        return count;
    }

    private record SectionMarkers(int? FirstInventory, int? Quickslots, int? SecondInventory);

    private static SectionMarkers FindSectionMarkers(byte[] data)
    {
        int? firstInv = FindAscii(data, "inventory", 0);
        int? quickslots = FindAscii(data, "__quickslots", 0);
        int? secondInv = firstInv is { } i ? FindAscii(data, "inventory", i + 1) : null;
        return new SectionMarkers(firstInv, quickslots, secondInv);
    }

    private static int? FindAscii(byte[] data, string s, int from)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        for (int i = from; i + bytes.Length <= data.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < bytes.Length; j++)
                if (data[i + j] != bytes[j]) { match = false; break; }
            if (match) return i;
        }
        return null;
    }

    private static string AssignContainer(int identifierOffset, SectionMarkers sections)
    {
        if (sections.FirstInventory == null) return "equipped";
        if (identifierOffset < sections.FirstInventory) return "equipped";
        if (sections.SecondInventory != null && identifierOffset >= sections.SecondInventory)
            return "ship_inventory";
        if (sections.Quickslots != null && identifierOffset > sections.Quickslots)
            return "__quickslots";
        return "inventory";
    }

    private static bool IsPrintable(byte b) => b >= 0x20 && b < 0x7F;
}
