using CodeAssist.Core.Caching;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class HotCacheRelativePathTests
{
    [Fact]
    public void ComputeRelativePath_ReturnsForwardSlashesRegardlessOfPlatform()
    {
        string root = Path.Combine(Path.GetTempPath(), "repo");
        string file = Path.Combine(root, "Editing", "Nested", "Deep.cs");

        string relative = HotCache.ComputeRelativePath(root, file);

        Assert.Equal("Editing/Nested/Deep.cs", relative);
        Assert.DoesNotContain('\\', relative);
    }

    [Fact]
    public void ComputeRelativePath_AgreesWithTheIndexerForm()
    {
        // The two writers must produce byte-identical keys or a delete issued by one
        // cannot match rows written by the other.
        string root = Path.Combine(Path.GetTempPath(), "repo");
        string file = Path.Combine(root, "Top.cs");

        Assert.Equal("Top.cs", HotCache.ComputeRelativePath(root, file));
    }
}
