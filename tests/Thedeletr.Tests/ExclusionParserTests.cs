using Thedeletr.Core;

namespace Thedeletr.Tests;

public class ExclusionParserTests
{
    [Fact]
    public void Parse_SplitsByCommaSemicolonAndNewLine()
    {
        var result = ExclusionParser.Parse("a, b;c\r\nd\n\n ; e ");

        Assert.Equal(["a", "b", "c", "d", "e"], result);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(ExclusionParser.Parse((string?)null));
        Assert.Empty(ExclusionParser.Parse("  "));
    }

    [Fact]
    public void Parse_MergesOptionValuesAndFile_WithoutDuplicates()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "fromFile\r\nshared;other,x");

            var result = ExclusionParser.Parse(["a;Shared", "b"], new FileInfo(file));

            Assert.Equal(["a", "Shared", "b", "fromFile", "other", "x"], result);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
