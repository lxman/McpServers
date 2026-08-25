using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class HotCacheSearchTests
{
    [Fact]
    public void SearchCachedFiles_ReturnsOnlyFilesFromTheRequestedRepository()
    {
        string requestedRoot = MakeRoot("requested");
        string otherRoot = MakeRoot("other");

        CachedFile requested = MakeCachedFile(requestedRoot, "Requested.cs", [0.8f, 0.2f]);
        CachedFile other = MakeCachedFile(otherRoot, "Other.cs", [1f, 0f]);

        List<HotCacheSearchResult> results = HotCache.SearchCachedFiles(
            [requested, other], [1f, 0f], requestedRoot, limit: 10, minScore: 0f,
            TestContext.Current.CancellationToken);

        HotCacheSearchResult result = Assert.Single(results);
        Assert.Equal("Requested.cs", result.CachedFile.RelativePath);
        Assert.Equal(requestedRoot, result.CachedFile.RepositoryRoot);
    }

    [Fact]
    public void SearchCachedFiles_RanksAgainstTheProvidedQueryBeforeApplyingTheLimit()
    {
        string root = MakeRoot("ranking");
        CachedFile wrongForQuery = MakeCachedFile(root, "Wrong.cs", [0f, 1f]);
        CachedFile bestForQuery = MakeCachedFile(root, "Best.cs", [1f, 0f]);

        List<HotCacheSearchResult> results = HotCache.SearchCachedFiles(
            [wrongForQuery, bestForQuery], [1f, 0f], root, limit: 1, minScore: 0f,
            TestContext.Current.CancellationToken);

        HotCacheSearchResult result = Assert.Single(results);
        Assert.Equal("Best.cs", result.CachedFile.RelativePath);
        Assert.Equal(1f, result.Score);
    }

    private static string MakeRoot(string name) =>
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "codeassist-hot-search", name));

    private static CachedFile MakeCachedFile(string root, string relativePath, float[] embedding)
    {
        string fullPath = Path.Combine(root, relativePath);
        var chunk = new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = fullPath,
            RelativePath = relativePath,
            Content = relativePath,
            StartLine = 1,
            EndLine = 1,
            ChunkType = "class",
            Language = "csharp",
            ContentHash = relativePath
        };

        return new CachedFile
        {
            FilePath = fullPath,
            RelativePath = relativePath,
            RepositoryRoot = root,
            Content = relativePath,
            ContentHash = relativePath,
            Language = "csharp",
            Chunks = [chunk],
            Embeddings = [embedding],
            LastModified = DateTime.UtcNow,
            CachedAt = DateTime.UtcNow
        };
    }
}
