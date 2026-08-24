using CodeAssist.Core.Caching;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class FileWatcherRootMatchingTests
{
    [Fact]
    public void IsUnderRoot_MatchesAFileInsideTheRoot()
    {
        Assert.True(FileWatcherService.IsUnderRoot(@"C:\src\repo\Editing\Foo.cs", @"C:\src\repo"));
    }

    [Fact]
    public void IsUnderRoot_RejectsASiblingRootSharingAPrefix()
    {
        // The bug this guards: "repo2" starts with "repo", so a bare StartsWith would resolve this
        // file to the wrong repository — and then delete against the wrong collection.
        Assert.False(FileWatcherService.IsUnderRoot(@"C:\src\repo2\Editing\Foo.cs", @"C:\src\repo"));
    }

    [Fact]
    public void IsUnderRoot_HandlesARootWithATrailingSeparator()
    {
        Assert.True(FileWatcherService.IsUnderRoot(@"C:\src\repo\Foo.cs", @"C:\src\repo\"));
        Assert.False(FileWatcherService.IsUnderRoot(@"C:\src\repo2\Foo.cs", @"C:\src\repo\"));
    }

    [Fact]
    public void IsUnderRoot_MatchesTheRootItself()
    {
        Assert.True(FileWatcherService.IsUnderRoot(@"C:\src\repo", @"C:\src\repo"));
    }

    [Fact]
    public void IsUnderRoot_IsCaseInsensitiveOnTheRootPortion()
    {
        Assert.True(FileWatcherService.IsUnderRoot(@"C:\SRC\repo\Foo.cs", @"C:\src\repo"));
    }
}
