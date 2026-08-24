using CodeAssist.Core.Caching;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class HotCacheRelativePathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "codeassist-hotcache-" + Guid.NewGuid().ToString("N"));

    public HotCacheRelativePathTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Editing", "Nested"));
        File.WriteAllText(Path.Combine(_root, "Editing", "Nested", "Deep.cs"), "class Deep {}");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

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
        // The two writers must produce byte-identical keys for the same file, or a delete issued by one
        // cannot match rows written by the other. Assert that against the indexer's real discovery
        // output, not a hardcoded string: a top-level file would pass even with normalization removed,
        // because GetRelativePath emits no separator for it.
        string file = Path.Combine(_root, "Editing", "Nested", "Deep.cs");

        List<string> discovered = RepositoryIndexer.DiscoverFiles(_root, ["*.cs"], []);
        string watcherForm = HotCache.ComputeRelativePath(_root, file);

        Assert.Equal("Editing/Nested/Deep.cs", watcherForm);
        Assert.Contains(watcherForm, discovered);
    }
}
