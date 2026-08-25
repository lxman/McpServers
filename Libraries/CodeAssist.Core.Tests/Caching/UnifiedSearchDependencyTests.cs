using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public sealed class UnifiedSearchDependencyTests
{
    [Fact]
    public void BuildQualifiedCalleeNames_ResolvesSafeSameTypeCalls()
    {
        UnifiedSearchHit hit = Hit([
            new CallReference { MethodName = "ProcessBatchAsync" },
            new CallReference
            {
                MethodName = "WriteAsync",
                ReceiverType = "CodeAssist.Core.Services.QdrantService"
            },
            new CallReference
            {
                MethodName = "ExplicitAsync",
                ReceiverType = "Ignored.Type",
                QualifiedName = "Exact.Target.ExplicitAsync"
            },
            new CallReference
            {
                MethodName = "UnknownAsync",
                ReceiverExpression = "otherService"
            }
        ]);

        List<string> names = UnifiedSearchService.BuildQualifiedCalleeNames([hit]);

        Assert.Equal([
            "CodeAssist.Core.Caching.L2PromotionService.ProcessBatchAsync",
            "CodeAssist.Core.Services.QdrantService.WriteAsync",
            "Exact.Target.ExplicitAsync"
        ], names);
    }

    [Fact]
    public void BuildQualifiedCalleeNames_DoesNotInferFromAggregateChunks()
    {
        UnifiedSearchHit hit = new()
        {
            Chunk = new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = @"C:\repo\L2PromotionService.cs",
                RelativePath = "L2PromotionService.cs",
                Content = "content",
                StartLine = 1,
                EndLine = 10,
                ChunkType = "class",
                SymbolName = "L2PromotionService",
                CallsOut = [new CallReference { MethodName = "ProcessBatchAsync" }],
                Language = "csharp",
                ContentHash = "hash",
                QualifiedName = "CodeAssist.Core.Caching.L2PromotionService"
            },
            Score = 1,
            Source = SearchSource.L2Qdrant,
            IsFresh = false
        };

        Assert.Empty(UnifiedSearchService.BuildQualifiedCalleeNames([hit]));
    }

    [Fact]
    public void ConsolidateDependencyFragments_ReturnsOneLogicalMethod()
    {
        UnifiedSearchHit part1 = DependencyPart(1, 10, 12, "line 10\nline 11\nline 12", "call one");
        UnifiedSearchHit part2 = DependencyPart(2, 12, 14, "line 12\nline 13\nline 14", "call two");
        UnifiedSearchHit unrelated = Hit([]);

        List<UnifiedSearchHit> consolidated = UnifiedSearchService.ConsolidateDependencyFragments(
            [part1, part2, unrelated]);

        Assert.Equal(2, consolidated.Count);
        UnifiedSearchHit method = consolidated[0];
        Assert.Equal("method", method.Chunk.ChunkType);
        Assert.Equal("ProcessBatchAsync", method.Chunk.SymbolName);
        Assert.Equal("L2PromotionService", method.Chunk.ParentSymbol);
        Assert.Equal(10, method.Chunk.StartLine);
        Assert.Equal(14, method.Chunk.EndLine);
        Assert.Equal(string.Join(Environment.NewLine,
            "line 10", "line 11", "line 12", "line 13", "line 14"), method.Chunk.Content);
        Assert.Equal(2, method.Chunk.CallsOut!.Count);
    }

    private static UnifiedSearchHit DependencyPart(
        int part,
        int startLine,
        int endLine,
        string content,
        string callName) => new()
    {
        Chunk = new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = @"C:\repo\L2PromotionService.cs",
            RelativePath = "L2PromotionService.cs",
            Content = content,
            StartLine = startLine,
            EndLine = endLine,
            ChunkType = $"method_part{part}",
            SymbolName = $"ProcessBatchAsync (part {part})",
            ParentSymbol = "ProcessBatchAsync",
            CallsOut = [new CallReference { MethodName = callName, Line = startLine }],
            Language = "csharp",
            ContentHash = "hash",
            QualifiedName = $"CodeAssist.Core.Caching.L2PromotionService.ProcessBatchAsync (part {part})"
        },
        Score = 1,
        Source = SearchSource.DependencyGraph,
        IsFresh = false,
        DependencyType = "callee"
    };

    private static UnifiedSearchHit Hit(IReadOnlyList<CallReference> calls) => new()
    {
        Chunk = new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = @"C:\repo\L2PromotionService.cs",
            RelativePath = "L2PromotionService.cs",
            Content = "content",
            StartLine = 1,
            EndLine = 10,
            ChunkType = "method",
            SymbolName = "ProcessPromotionQueueAsync",
            ParentSymbol = "L2PromotionService",
            CallsOut = calls,
            Language = "csharp",
            ContentHash = "hash",
            QualifiedName = "CodeAssist.Core.Caching.L2PromotionService.ProcessPromotionQueueAsync"
        },
        Score = 1,
        Source = SearchSource.L2Qdrant,
        IsFresh = false
    };
}
