using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class KnownTagAnnotationsTests
{
    [Fact]
    public void Lookup_SlotMetaTimestampTags_ReturnFriendlyNames()
    {
        Assert.Equal("Month", KnownTagAnnotations.Lookup("93_meta.sav", 5));
        Assert.Equal("Day", KnownTagAnnotations.Lookup("93_meta.sav", 6));
        Assert.Equal("Year", KnownTagAnnotations.Lookup("93_meta.sav", 7));
        Assert.Equal("Hour", KnownTagAnnotations.Lookup("93_meta.sav", 8));
        Assert.Equal("Minute", KnownTagAnnotations.Lookup("93_meta.sav", 9));
        Assert.Equal("Second", KnownTagAnnotations.Lookup("93_meta.sav", 10));
    }

    [Fact]
    public void Lookup_UnknownTagOnKnownFile_ReturnsNull()
    {
        Assert.Null(KnownTagAnnotations.Lookup("93_meta.sav", 99));
    }

    [Fact]
    public void Lookup_UnknownFile_ReturnsNull()
    {
        Assert.Null(KnownTagAnnotations.Lookup("not_a_real_file.sav", 1));
    }

    [Fact]
    public void Lookup_FilenameIsCaseInsensitive()
    {
        Assert.Equal("Year", KnownTagAnnotations.Lookup("93_META.SAV", 7));
    }

    [Fact]
    public void Lookup_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(KnownTagAnnotations.Lookup("", 1));
        Assert.Null(KnownTagAnnotations.Lookup(null!, 1));
    }
}
