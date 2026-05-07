using System.Text;
using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.Core.SaveContent;

public static class SaveSummaryBuilder
{
    public static SaveSummary Build(SaveSlot slot, ItemCatalog catalog)
    {
        var warnings = new List<string>();

        var characterName = TryReadCharacterName(slot, warnings);
        var savedAt = TryReadSavedTimestamp(slot, warnings);
        var inventories = TryExtractInventories(slot, catalog, warnings);
        var ships = slot.Files
            .Where(f => f.FileName.StartsWith("ship_", StringComparison.OrdinalIgnoreCase)
                     && f.FileName.EndsWith(".vx", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.FileName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SaveSummary(slot, characterName, savedAt, inventories, ships, warnings, catalog.IconAtlas);
    }

    // 93_meta.sav holds the character name as a plain ASCII run inside one of
    // its TLV value blobs. We don't (yet) know which tag it's keyed under, so
    // we scan the decompressed bytes for the longest printable-ASCII run that
    // starts with a letter — that empirically picks up the name.
    private static string? TryReadCharacterName(SaveSlot slot, List<string> warnings)
    {
        var meta = slot.Files.FirstOrDefault(f => f.FileName == "93_meta.sav");
        if (meta == null) return null;
        var blob = SaveBlobReader.ReadFile(meta.FullPath);
        if (blob.DecompressedBytes == null) { warnings.Add("Could not decompress 93_meta.sav."); return null; }

        string? best = null;
        var data = blob.DecompressedBytes;
        int i = 0;
        while (i < data.Length)
        {
            if (!IsLetter(data[i])) { i++; continue; }
            int start = i;
            while (i < data.Length && IsPrintable(data[i])) i++;
            int len = i - start;
            if (len >= 3 && (best == null || len > best.Length))
                best = Encoding.ASCII.GetString(data, start, len);
        }
        return best;
    }

    // 93_meta.sav has tags 5-10 mapped to local-time month, day, year, hour,
    // minute, second (verified against file mtime). Pull them by walking the
    // top-level entries. Returns null if we can't make a valid DateTime.
    private static DateTime? TryReadSavedTimestamp(SaveSlot slot, List<string> warnings)
    {
        var meta = slot.Files.FirstOrDefault(f => f.FileName == "93_meta.sav");
        if (meta == null) return null;
        var blob = SaveBlobReader.ReadFile(meta.FullPath);
        if (blob.DecompressedBytes == null) return null;

        var doc = TlvParser.Parse(blob.DecompressedBytes);
        int? month = null, day = null, year = null, hour = null, minute = null, second = null;
        foreach (var e in doc.Entries)
        {
            if (e.Kind != TlvValueKind.Int32 || e.RawData.Length != 4) continue;
            int v = BitConverter.ToInt32(e.RawData);
            switch (e.Tag)
            {
                case 5: month = v; break;
                case 6: day = v; break;
                case 7: year = v; break;
                case 8: hour = v; break;
                case 9: minute = v; break;
                case 10: second = v; break;
            }
        }
        if (month is null or < 1 or > 12) return null;
        if (day is null or < 1 or > 31) return null;
        if (year is null or < 1970 or > 9999) return null;

        try
        {
            // The fields are local time per game; treat as Unspecified and let UI ToLocalTime if needed.
            return new DateTime(year.Value, month.Value, day.Value,
                                hour ?? 0, minute ?? 0, second ?? 0,
                                DateTimeKind.Local);
        }
        catch (ArgumentOutOfRangeException)
        {
            warnings.Add("93_meta.sav timestamp fields out of range.");
            return null;
        }
    }

    private static IReadOnlyList<InventoryContainer> TryExtractInventories(SaveSlot slot, ItemCatalog catalog, List<string> warnings)
    {
        var clientState = slot.Files.FirstOrDefault(f => f.FileName == "93_client_state.sav");
        if (clientState == null) { warnings.Add("Slot has no 93_client_state.sav."); return Array.Empty<InventoryContainer>(); }
        var blob = SaveBlobReader.ReadFile(clientState.FullPath);
        if (blob.DecompressedBytes == null)
        {
            warnings.Add(blob.ErrorMessage ?? "Could not decompress 93_client_state.sav.");
            return Array.Empty<InventoryContainer>();
        }
        var inventories = InventoryExtractor.Extract(blob.DecompressedBytes, catalog);
        if (inventories.Count == 0) warnings.Add("No inventory containers detected.");
        if (catalog.IsEmpty) warnings.Add("Item catalog not loaded — names use fallback humanization.");
        return inventories;
    }

    private static bool IsPrintable(byte b) => b >= 0x20 && b < 0x7F;
    private static bool IsLetter(byte b) => (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z');
}
