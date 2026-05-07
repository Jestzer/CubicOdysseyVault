using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class ItemCatalogTests
{
    [Fact]
    public void Humanize_StripsCategoryPrefixAndExtractsTier()
    {
        Assert.Equal("Suit (Tier 2)", ItemCatalog.HumanizeIdentifier("cloth.suit.2"));
    }

    [Fact]
    public void Humanize_TitleCasesUnderscoreSegments()
    {
        Assert.Equal("Mining Laser (Tier 3)", ItemCatalog.HumanizeIdentifier("wep.mining_laser.3"));
    }

    [Fact]
    public void Humanize_LongIdentifierWithoutTrailingTier()
    {
        Assert.Equal("Karve Engine Space Mk1", ItemCatalog.HumanizeIdentifier("comp.karve.engine.space.mk1"));
    }

    [Fact]
    public void Humanize_PrefersExplicitTierArgument()
    {
        Assert.Equal("Battery (Tier 5)", ItemCatalog.HumanizeIdentifier("res.battery", tier: 5));
    }

    [Fact]
    public void Humanize_NoTierAvailable_OmitsTierSuffix()
    {
        Assert.Equal("Wiring", ItemCatalog.HumanizeIdentifier("res.wiring"));
    }

    [Fact]
    public void Humanize_UnknownPrefix_KeepsAllSegments()
    {
        // Unknown prefix is preserved — better to over-display than to drop info
        // about a category we don't recognize.
        Assert.Equal("Xyz Unknown Prefix Item", ItemCatalog.HumanizeIdentifier("xyz.unknown_prefix.item"));
    }

    [Fact]
    public void EmptyCatalog_LookupReturnsNull()
    {
        Assert.Null(ItemCatalog.Empty.Lookup("anything"));
    }

    [Fact]
    public void LoadFrom_NonExistentDirectory_ReturnsEmpty()
    {
        var catalog = ItemCatalog.LoadFrom(Path.Combine(Path.GetTempPath(), $"covtest-no-{Guid.NewGuid():N}"));
        Assert.True(catalog.IsEmpty);
    }

    [Fact]
    public void LoadFrom_TempDirWithOneCfg_ParsesIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"covtest-cat-{Guid.NewGuid():N}");
        try
        {
            var configDir = Path.Combine(dir, "data", "configs", "items");
            Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "TEST.cfg"),
                "ItemCfg\n{\n  identifier \"res.test.1\"\n  type RESOURCE\n  tier 1\n}\n");

            var catalog = ItemCatalog.LoadFrom(dir);
            Assert.False(catalog.IsEmpty);
            Assert.NotNull(catalog.Lookup("res.test.1"));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Classifier_IdentifierPrefixWins()
    {
        // ClassifyByIdentifier maps wep.* → Weapon even when its .cfg type was UTILS or DRONE_GEAR.
        Assert.Equal(ItemCategory.Weapon, ItemCategoryClassifier.ClassifyByIdentifier("wep.mining_laser.3"));
        Assert.Equal(ItemCategory.Resource, ItemCategoryClassifier.ClassifyByIdentifier("res.battery.3"));
        Assert.Equal(ItemCategory.Equipment, ItemCategoryClassifier.ClassifyByIdentifier("cloth.suit.2"));
        Assert.Equal(ItemCategory.ShipComponent, ItemCategoryClassifier.ClassifyByIdentifier("comp.karve.shield.mk1"));
        Assert.Equal(ItemCategory.Deployable, ItemCategoryClassifier.ClassifyByIdentifier("dpl.led.small"));
        Assert.Equal(ItemCategory.Other, ItemCategoryClassifier.ClassifyByIdentifier("zzz.unknown"));
    }
}
