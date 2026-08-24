using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class DiscoverFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "codeassist-discover-" + Guid.NewGuid().ToString("N"));

    public DiscoverFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Editing", "Nested"));
        File.WriteAllText(Path.Combine(_root, "Top.cs"), "class Top {}");
        File.WriteAllText(Path.Combine(_root, "Editing", "Mid.cs"), "class Mid {}");
        File.WriteAllText(Path.Combine(_root, "Editing", "Nested", "Deep.cs"), "class Deep {}");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void DiscoverFiles_ReturnsForwardSlashPathsOnly()
    {
        List<string> files = RepositoryIndexer.DiscoverFiles(_root, ["*.cs"], []);

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.DoesNotContain('\\', f));
    }

    [Fact]
    public void DiscoverFiles_FindsNestedFilesWithForwardSlashSeparators()
    {
        List<string> files = RepositoryIndexer.DiscoverFiles(_root, ["*.cs"], []);

        Assert.Contains("Editing/Nested/Deep.cs", files);
        Assert.Contains("Editing/Mid.cs", files);
        Assert.Contains("Top.cs", files);
    }
}
