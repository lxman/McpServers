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
