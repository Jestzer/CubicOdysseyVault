using CubicOdysseyVault.Core.Steam;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class LibraryFoldersVdfParserTests
{
    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(LibraryFoldersVdfParser.ParseLibraryPaths(""));
    }

    [Fact]
    public void WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Empty(LibraryFoldersVdfParser.ParseLibraryPaths("   \n\t  "));
    }

    [Fact]
    public void SingleLibrary_ParsesPath()
    {
        var vdf = "\"libraryfolders\"\n{\n  \"0\"\n  {\n    \"path\" \"/home/user/.local/share/Steam\"\n  }\n}";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(new[] { "/home/user/.local/share/Steam" }, paths);
    }

    [Fact]
    public void MultipleLibraries_ParsesInDeclaredOrder()
    {
        var vdf = "\"libraryfolders\"\n{\n  \"0\" { \"path\" \"/a\" }\n  \"1\" { \"path\" \"/b\" }\n  \"2\" { \"path\" \"/c\" }\n}";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(new[] { "/a", "/b", "/c" }, paths);
    }

    [Fact]
    public void RealMachineLayout_ParsesAllFour()
    {
        // Reflects the real shape on this user's machine: nested "apps" blocks,
        // extra metadata keys (label, contentid), four declared libraries.
        var vdf = @"
""libraryfolders""
{
    ""0""
    {
        ""path""        ""/home/james/.local/share/Steam""
        ""label""       """"
        ""contentid""   ""4676277068289392616""
        ""apps""
        {
            ""228980""  ""449977428""
        }
    }
    ""1""
    {
        ""path""        ""/run/media/james/SSD/Program Files (x86)/Steam""
        ""apps""
        {
            ""320""     ""3301997136""
            ""440""     ""31999440827""
        }
    }
    ""2""
    {
        ""path""        ""/mnt/extra/Steam""
    }
    ""3""
    {
        ""path""        ""/another""
    }
}
";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(
            new[]
            {
                "/home/james/.local/share/Steam",
                "/run/media/james/SSD/Program Files (x86)/Steam",
                "/mnt/extra/Steam",
                "/another",
            },
            paths);
    }

    [Fact]
    public void IgnoresLineComments()
    {
        var vdf = "// header\n\"libraryfolders\"\n{\n  // section\n  \"0\" { \"path\" \"/x\" }  // trailing\n}\n";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(new[] { "/x" }, paths);
    }

    [Fact]
    public void HandlesCrlfAndTabs()
    {
        var vdf = "\"libraryfolders\"\r\n{\r\n\t\"0\"\r\n\t{\r\n\t\t\"path\"\t\"/y\"\r\n\t}\r\n}\r\n";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(new[] { "/y" }, paths);
    }

    [Fact]
    public void WindowsBackslashEscapes_AreUnescaped()
    {
        var vdf = "\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Program Files (x86)\\\\Steam\" } }";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(new[] { @"C:\Program Files (x86)\Steam" }, paths);
    }

    [Fact]
    public void NestedAppsBlock_DoesNotShadowOuterPath()
    {
        // Inner "path" inside an "apps" block must not be picked up as a library path.
        var vdf = "\"libraryfolders\" { \"0\" { \"path\" \"/correct\" \"apps\" { \"path\" \"/wrong\" } } }";
        var paths = LibraryFoldersVdfParser.ParseLibraryPaths(vdf);
        Assert.Equal(new[] { "/correct" }, paths);
    }

    [Fact]
    public void MalformedUnclosedBrace_Throws()
    {
        var vdf = "\"libraryfolders\" { \"0\" { \"path\" \"/x\"";
        Assert.Throws<FormatException>(() => LibraryFoldersVdfParser.ParseLibraryPaths(vdf));
    }

    [Fact]
    public void NotLibraryFoldersTopLevel_ReturnsEmpty()
    {
        // Robustness: don't throw if Steam writes something we don't recognize at top level.
        var vdf = "\"otherfile\" { \"key\" \"value\" }";
        Assert.Empty(LibraryFoldersVdfParser.ParseLibraryPaths(vdf));
    }
}
