using CodeAssist.Core.Caching;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class WatchPatternMatcherTests
{
    [Theory]
    [InlineData("Top.cs", true)]
    [InlineData("src/Nested.cs", true)]
    [InlineData("src/Nested.py", false)]
    [InlineData("generated/Output.cs", false)]
    [InlineData("src/generated/Output.cs", false)]
    public void IsMatch_HonorsRepositoryIncludesAndExcludes(string relativePath, bool expected)
    {
        var matcher = new WatchPatternMatcher(["*.cs"], ["**/generated/**"]);

        Assert.Equal(expected, matcher.IsMatch(relativePath));
    }

    [Fact]
    public void IsMatch_SupportsCustomFileTypesAcceptedByTheIndexPatterns()
    {
        var matcher = new WatchPatternMatcher(["docs/*.md"], []);

        Assert.True(matcher.IsMatch("docs/guide.md"));
        Assert.False(matcher.IsMatch("src/guide.md"));
    }

    [Fact]
    public void IsMatch_NormalizesWindowsSeparators()
    {
        var matcher = new WatchPatternMatcher(["src/**/*.cs"], []);

        Assert.True(matcher.IsMatch(@"src\nested\File.cs"));
    }
}
