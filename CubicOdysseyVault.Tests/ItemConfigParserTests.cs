using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class ItemConfigParserTests
{
    [Fact]
    public void Parse_RealConfigShape_PullsExpectedFields()
    {
        const string text = """
ItemCfg
{
    id                      15
    identifier              "cloth.suit.2"
    type                    GEAR
    tier                    2
    durability              100
    inv_frame               67
    title_string            "STR_SUIT_2"
    description_string      "STR_SUIT_2_DESC"
    stack_size              1
    recycle_value           2
    base_price              69
}
""";
        var meta = ItemConfigParser.Parse(text);
        Assert.NotNull(meta);
        Assert.Equal("cloth.suit.2", meta!.Identifier);
        Assert.Equal("GEAR", meta.TypeRaw);
        Assert.Equal(2, meta.Tier);
        Assert.Equal("STR_SUIT_2", meta.TitleString);
        Assert.Equal(67, meta.InvFrame);
        Assert.Equal(1, meta.StackSize);
        Assert.Equal(2, meta.RecyclePrice);
        Assert.Equal(69, meta.BasePrice);
    }

    [Fact]
    public void Parse_TolerantOfTrailingComments()
    {
        const string text = """
ItemCfg {
    identifier "wep.test.1"   // this is a comment
    type WEAPON_RANGED        // tier comes next
    tier 4
}
""";
        var meta = ItemConfigParser.Parse(text);
        Assert.NotNull(meta);
        Assert.Equal("wep.test.1", meta!.Identifier);
        Assert.Equal("WEAPON_RANGED", meta.TypeRaw);
        Assert.Equal(4, meta.Tier);
    }

    [Fact]
    public void Parse_NotItemCfg_ReturnsNull()
    {
        const string text = "SomeOther\n{\n    identifier \"x.y.z\"\n}";
        Assert.Null(ItemConfigParser.Parse(text));
    }

    [Fact]
    public void Parse_MissingIdentifier_ReturnsNull()
    {
        const string text = "ItemCfg\n{\n    type GEAR\n    tier 1\n}";
        Assert.Null(ItemConfigParser.Parse(text));
    }

    [Fact]
    public void Parse_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(ItemConfigParser.Parse(""));
        Assert.Null(ItemConfigParser.Parse("   \n\t   "));
    }
}
