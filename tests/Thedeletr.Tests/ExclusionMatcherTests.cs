using Thedeletr.Core;

namespace Thedeletr.Tests;

public class ExclusionMatcherTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "root");

    private static bool Excluded(string pattern, string relativePath) =>
        new ExclusionMatcher([pattern], Root).IsExcluded(Path.Combine(Root, relativePath));

    [Theory]
    [InlineData("keep", @"a\keep", true)]
    [InlineData("KEEP", @"a\keep", true)]
    [InlineData("keep", @"a\keeper", false)]
    [InlineData("keep", @"keep\child", false)]
    [InlineData("keep*", @"a\keeper", true)]
    [InlineData("keep*", @"a\xkeep", false)]
    [InlineData("*keep", @"a\xkeep", true)]
    [InlineData("*keep", @"a\keeper", false)]
    [InlineData("*keep*", @"a\xkeepx", true)]
    [InlineData("*keep*", @"a\other", false)]
    [InlineData("*.log", @"a\file.log", true)]
    [InlineData("*.log", @"a\file.txt", false)]
    public void NamePatterns(string pattern, string relativePath, bool expected) =>
        Assert.Equal(expected, Excluded(pattern, relativePath));

    [Fact]
    public void FullPath_MatchesExactPathOnly()
    {
        var pattern = Path.Combine(Root, "a", "keep");

        Assert.True(Excluded(pattern, @"a\keep"));
        Assert.True(Excluded(pattern + Path.DirectorySeparatorChar, @"a\keep"));
        Assert.False(Excluded(pattern, @"b\keep"));
    }

    [Fact]
    public void RelativePath_IsResolvedAgainstRoot()
    {
        Assert.True(Excluded(@"a\keep", @"a\keep"));
        Assert.True(Excluded("a/keep", @"a\keep"));
        Assert.False(Excluded(@"a\keep", @"b\a\keep"));
    }

    [Fact]
    public void PathPattern_SupportsWildcards()
    {
        Assert.True(Excluded(@"a\*\cache", @"a\x\cache"));
        Assert.False(Excluded(@"a\*\cache", @"b\x\cache"));
    }

    [Fact]
    public void Empty_ExcludesNothing()
    {
        var matcher = new ExclusionMatcher([], Root);

        Assert.True(matcher.IsEmpty);
        Assert.False(matcher.IsExcluded(Path.Combine(Root, "anything")));
    }
}
