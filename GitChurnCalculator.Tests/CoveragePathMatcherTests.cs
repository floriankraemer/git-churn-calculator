using GitChurnCalculator.Services;
using Xunit;

namespace GitChurnCalculator.Tests;

public class CoveragePathMatcherTests
{
    [Fact]
    public void MapToGitFiles_FileNameOnly_MatchesWhenGitPathEndsWithSameFileName()
    {
        var coverage = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Foo.cs"] = 88.0,
        };
        var gitFiles = new List<string> { "src/nested/Foo.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(88.0, mapped["src/nested/Foo.cs"]);
    }

    [Fact]
    public void MapToGitFiles_ExactMatch_WinsOverSuffix()
    {
        var coverage = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["src/a.cs"] = 10.0,
        };
        var gitFiles = new List<string> { "src/a.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(10.0, mapped["src/a.cs"]);
    }

    [Fact]
    public void MapToGitFiles_DuplicateFileName_UsesFirstGitFileInOrder()
    {
        var coverage = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Foo.cs"] = 55.0,
        };
        var gitFiles = new List<string> { "src/a/Foo.cs", "src/b/Foo.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(55.0, mapped["src/a/Foo.cs"]);
    }

    [Fact]
    public void MapToGitFiles_AbsoluteCoveragePath_PrefersEarlierGitFileAmongSuffixMatches()
    {
        var coverage = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:/project/src/Foo.cs"] = 33.0,
        };
        var gitFiles = new List<string> { "Foo.cs", "src/Foo.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(33.0, mapped["Foo.cs"]);
    }
}
