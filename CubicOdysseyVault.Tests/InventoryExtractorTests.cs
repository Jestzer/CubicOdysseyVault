using System.Linq;
using System.Text;
using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class InventoryExtractorTests
{
    [Fact]
    public void Extract_NoData_ReturnsEmpty()
    {
        Assert.Empty(InventoryExtractor.Extract(Array.Empty<byte>(), ItemCatalog.Empty));
    }

    [Fact]
    public void Extract_OneItemRecordWithCount_ParsesIdentifierAndCount()
    {
        // Synthesize one item record: [tag=1 type=12 string "res.battery.3"][tag=2 float32 100][tag=3 int32 5]
        var bytes = BuildItemRecord("res.battery.3", durability: 100f, count: 5);
        // Wrap with leading "inventory" marker so the item is grouped as Inventory.
        var data = Concat(AsciiZ("inventory"), bytes);

        var result = InventoryExtractor.Extract(data, ItemCatalog.Empty);
        var inv = result.Single(c => c.Name == "inventory");
        var item = inv.Items.Single();
        Assert.Equal("res.battery.3", item.Identifier);
        Assert.Equal(5, item.Count);
        Assert.Equal(ItemCategory.Resource, item.Category);
    }

    [Fact]
    public void Extract_ItemBeforeInventoryMarker_GoesToEquipped()
    {
        var item = BuildItemRecord("cloth.suit.2", 100f, 1);
        var data = Concat(item, AsciiZ("inventory"));

        var result = InventoryExtractor.Extract(data, ItemCatalog.Empty);
        Assert.Contains(result, c => c.Name == "equipped" && c.Items.Count == 1);
    }

    [Fact]
    public void Extract_BetweenQuickslotsAndSecondInventory_GoesToQuickslots()
    {
        // Layout: <inventory> <__quickslots> <item> <inventory>
        var item = BuildItemRecord("wep.rifle.sma15e.3", 100f, 1);
        var data = Concat(
            AsciiZ("inventory"),
            AsciiZ("__quickslots"),
            item,
            AsciiZ("inventory"));

        var result = InventoryExtractor.Extract(data, ItemCatalog.Empty);
        Assert.Contains(result, c => c.Name == "__quickslots" && c.Items.Count == 1);
    }

    [Fact]
    public void Extract_AfterSecondInventoryMarker_GoesToShipCargo()
    {
        var item = BuildItemRecord("res.iron.ingot", 100f, 12);
        var data = Concat(
            AsciiZ("inventory"),
            AsciiZ("inventory"),
            item);

        var result = InventoryExtractor.Extract(data, ItemCatalog.Empty);
        Assert.Contains(result, c => c.Name == "ship_inventory" && c.Items.Count == 1 && c.Items[0].Count == 12);
    }

    [Fact]
    public void Extract_PatternMismatch_FallsBackToCountOne()
    {
        // Build an item that doesn't have the expected 02 00 0a 00 ... pattern after the id:
        // Just write the identifier string with a length prefix; nothing else.
        var nameBytes = Encoding.ASCII.GetBytes("res.tusks");
        var record = new List<byte>();
        record.AddRange(new byte[] { 0x01, 0x00, 0x0C, 0x00 }); // tag=1 type=12
        int len = 2 + nameBytes.Length + 1;
        record.AddRange(BitConverter.GetBytes(len));
        record.AddRange(BitConverter.GetBytes((ushort)(nameBytes.Length + 1))); // u16 char count incl null
        record.AddRange(nameBytes);
        record.Add(0); // null
        // No tag-2/tag-3 to follow — extractor should default count=1
        record.AddRange(new byte[] { 0xFF, 0xFF, 0xFF }); // junk follow

        var data = Concat(AsciiZ("inventory"), record.ToArray());
        var result = InventoryExtractor.Extract(data, ItemCatalog.Empty);
        var item = result.Single(c => c.Name == "inventory").Items.Single();
        Assert.Equal("res.tusks", item.Identifier);
        Assert.Equal(1, item.Count);
    }

    private static byte[] BuildItemRecord(string identifier, float durability, int count)
    {
        var nameBytes = Encoding.ASCII.GetBytes(identifier);
        var record = new List<byte>();

        // Identifier entry: tag=1 type=12 length=(2+name+1) data=[u16 count incl null][name][null]
        record.AddRange(new byte[] { 0x01, 0x00, 0x0C, 0x00 });
        int idDataLen = 2 + nameBytes.Length + 1;
        record.AddRange(BitConverter.GetBytes(idDataLen));
        record.AddRange(BitConverter.GetBytes((ushort)(nameBytes.Length + 1)));
        record.AddRange(nameBytes);
        record.Add(0);

        // Durability entry: tag=2 type=10 length=4 data=<float32>
        record.AddRange(new byte[] { 0x02, 0x00, 0x0A, 0x00, 0x04, 0x00, 0x00, 0x00 });
        record.AddRange(BitConverter.GetBytes(durability));

        // Count entry: tag=3 type=4 length=4 data=<int32>
        record.AddRange(new byte[] { 0x03, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x00 });
        record.AddRange(BitConverter.GetBytes(count));

        return record.ToArray();
    }

    private static byte[] AsciiZ(string s) => Concat(Encoding.ASCII.GetBytes(s), new byte[] { 0 });

    private static byte[] Concat(params byte[][] arrays)
    {
        var total = arrays.Sum(a => a.Length);
        var result = new byte[total];
        int off = 0;
        foreach (var a in arrays) { Buffer.BlockCopy(a, 0, result, off, a.Length); off += a.Length; }
        return result;
    }
}
