using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class UnifiedSearchMergeTests
{
    [Fact]
    public void MergeResults_DropsStaleL2HitWhenHotFileHasNoMatchingFreshChunk()
    {
        CodeChunk stale = MakeChunk("OldName", 10, "stale");
        CachedFile hotFile = MakeCachedFile(MakeChunk("NewName", 30, "fresh"));

        List<UnifiedSearchHit> merged = UnifiedSearchService.MergeResults(
            [], [MakeHit(stale, 0.9f, false)], 10, _ => hotFile);

        Assert.Empty(merged);
    }

    [Fact]
    public void MergeResults_UsesHotCacheEvenWhenFileProducedNoL1Candidate()
    {
        CodeChunk stale = MakeChunk("Run", 10, "stale");
        CodeChunk fresh = MakeChunk("Run", 10, "fresh");
        CachedFile hotFile = MakeCachedFile(fresh);

        UnifiedSearchHit merged = Assert.Single(UnifiedSearchService.MergeResults(
            [], [MakeHit(stale, 0.9f, false)], 10, _ => hotFile));

        Assert.Same(fresh, merged.Chunk);
        Assert.True(merged.IsFresh);
        Assert.Equal(SearchSource.L2WithL1Content, merged.Source);
    }

    private static UnifiedSearchHit MakeHit(CodeChunk chunk, float score, bool fresh) => new()
    {
        Chunk = chunk,
        Score = score,
        Source = fresh ? SearchSource.L1HotCache : SearchSource.L2Qdrant,
        IsFresh = fresh
    };

    private static CodeChunk MakeChunk(string symbol, int startLine, string content) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = "C:/repo/Worker.cs",
        RelativePath = "Worker.cs",
        Content = content,
        StartLine = startLine,
        EndLine = startLine + 5,
        ChunkType = "method",
        SymbolName = symbol,
        Language = "csharp",
        ContentHash = Guid.NewGuid().ToString("N")
    };

    private static CachedFile MakeCachedFile(params CodeChunk[] chunks) => new()
    {
        FilePath = "C:/repo/Worker.cs",
        RelativePath = "Worker.cs",
        RepositoryRoot = "C:/repo",
        Content = string.Join('\n', chunks.Select(chunk => chunk.Content)),
        ContentHash = Guid.NewGuid().ToString("N"),
        Language = "csharp",
        Chunks = chunks.ToList(),
        Embeddings = [],
        LastModified = DateTime.UtcNow,
        CachedAt = DateTime.UtcNow
    };
}
