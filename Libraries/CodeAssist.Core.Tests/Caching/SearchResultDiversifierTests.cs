using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class SearchResultDiversifierTests
{
    [Fact]
    public void Diversify_PrefersSpecificMethodOverOverlappingClassChunk()
    {
        UnifiedSearchHit containingClass = MakeHit("Service", "class_part3", 1, 100, 0.80f);
        UnifiedSearchHit method = MakeHit("RunAsync", "method", 30, 50, 0.79f);

        UnifiedSearchHit selected = Assert.Single(
            SearchResultDiversifier.Diversify([containingClass, method], 10));

        Assert.Equal("RunAsync", selected.Chunk.SymbolName);
    }

    [Fact]
    public void Diversify_LimitsRepeatedResultsFromOneFile()
    {
        UnifiedSearchHit[] candidates =
        [
            MakeHit("First", "method", 1, 10, 0.9f),
            MakeHit("Second", "method", 20, 30, 0.8f),
            MakeHit("Third", "method", 40, 50, 0.7f),
            MakeHit("Other", "method", 1, 10, 0.6f, "other.cs")
        ];

        List<UnifiedSearchHit> selected = SearchResultDiversifier.Diversify(candidates, 10);

        Assert.Equal(3, selected.Count);
        Assert.Equal(2, selected.Count(hit => hit.Chunk.RelativePath == "test.cs"));
        Assert.Contains(selected, hit => hit.Chunk.RelativePath == "other.cs");
    }

    [Theory]
    [InlineData("class_part12", "class")]
    [InlineData("method_part3", "method")]
    [InlineData("file_segment", "file_segment")]
    public void BaseChunkType_StripsOnlyNumericPartSuffixes(string chunkType, string expected)
    {
        Assert.Equal(expected, SearchResultDiversifier.BaseChunkType(chunkType));
    }

    [Theory]
    [InlineData("ProcessAsync (part 12)", "ProcessAsync")]
    [InlineData("ProcessAsync (partial)", "ProcessAsync (partial)")]
    public void RemovePartSuffix_StripsOnlyGeneratedPartSuffixes(string value, string expected)
    {
        Assert.Equal(expected, SearchResultDiversifier.RemovePartSuffix(value));
    }

    [Fact]
    public void ResolveFreshExactMatches_ReplacesIndexedPayloadWithHotCacheChunk()
    {
        CodeChunk indexed = MakeChunk("RunAsync", "stale content");
        CodeChunk fresh = MakeChunk("RunAsync", "fresh content");
        CachedFile cachedFile = MakeCachedFile(fresh);

        ExactSymbolMatch match = Assert.Single(SearchResultDiversifier.ResolveFreshExactMatches(
            [new SearchResult { Chunk = indexed, Score = 0f }],
            "RunAsync",
            _ => cachedFile));

        Assert.True(match.IsFresh);
        Assert.Same(fresh, match.Chunk);
    }

    [Fact]
    public void ResolveFreshExactMatches_DropsIndexedSymbolRemovedFromHotFile()
    {
        CodeChunk indexed = MakeChunk("OldName", "stale content");
        CachedFile cachedFile = MakeCachedFile(MakeChunk("NewName", "fresh content"));

        List<ExactSymbolMatch> matches = SearchResultDiversifier.ResolveFreshExactMatches(
            [new SearchResult { Chunk = indexed, Score = 0f }],
            "OldName",
            _ => cachedFile);

        Assert.Empty(matches);
    }

    private static UnifiedSearchHit MakeHit(
        string symbol,
        string chunkType,
        int startLine,
        int endLine,
        float score,
        string path = "test.cs")
    {
        return new UnifiedSearchHit
        {
            Chunk = new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = path,
                RelativePath = path,
                Content = symbol,
                StartLine = startLine,
                EndLine = endLine,
                ChunkType = chunkType,
                SymbolName = symbol,
                Language = "csharp",
                ContentHash = Guid.NewGuid().ToString("N")
            },
            Score = score,
            Source = SearchSource.L2Qdrant,
            IsFresh = false
        };
    }

    private static CodeChunk MakeChunk(string symbol, string content)
    {
        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = "C:/repo/test.cs",
            RelativePath = "test.cs",
            Content = content,
            StartLine = 1,
            EndLine = 10,
            ChunkType = "method",
            SymbolName = symbol,
            Language = "csharp",
            ContentHash = Guid.NewGuid().ToString("N")
        };
    }

    private static CachedFile MakeCachedFile(params CodeChunk[] chunks)
    {
        return new CachedFile
        {
            FilePath = "C:/repo/test.cs",
            RelativePath = "test.cs",
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
}
