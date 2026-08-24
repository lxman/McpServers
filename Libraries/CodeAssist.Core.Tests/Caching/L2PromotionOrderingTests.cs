using CodeAssist.Core.Caching;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class L2PromotionOrderingTests
{
    private static CachedFile MakeCachedFile(string relativePath, int chunkCount)
    {
        var chunks = new List<CodeChunk>();
        var embeddings = new List<float[]>();
        for (var i = 0; i < chunkCount; i++)
        {
            chunks.Add(new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = @"C:\repo\" + relativePath.Replace('/', '\\'),
                RelativePath = relativePath,
                Content = $"chunk {i}",
                StartLine = i * 10,
                EndLine = i * 10 + 5,
                ChunkType = "method",
                Language = "csharp",
                ContentHash = $"hash{i}"
            });
            embeddings.Add([0.1f, 0.2f]);
        }

        return new CachedFile
        {
            FilePath = @"C:\repo\" + relativePath.Replace('/', '\\'),
            RelativePath = relativePath,
            RepositoryRoot = @"C:\repo",
            Content = "content",
            ContentHash = "filehash",
            Language = "csharp",
            Chunks = chunks,
            Embeddings = embeddings,
            LastModified = DateTime.UtcNow,
            CachedAt = DateTime.UtcNow
        };
    }

    private static L2PromotionService MakeService(FakeQdrantWriter writer, HotCache hotCache) =>
        new(hotCache,
            writer,
            Options.Create(new CodeAssistOptions { EnableL2Promotion = true }),
            NullLogger<L2PromotionService>.Instance);

    [Fact]
    public async Task PromotingAFile_DeletesItsPriorChunksBeforeUpserting()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 3), "myrepo");

        int deleteIndex = writer.Calls.IndexOf("delete:Editing/Foo.cs");
        int upsertIndex = writer.Calls.FindIndex(c => c.StartsWith("upsert:", StringComparison.Ordinal));

        Assert.True(deleteIndex >= 0, "the file's prior chunks must be deleted");
        Assert.True(upsertIndex >= 0, "the new chunks must be upserted");
        Assert.True(deleteIndex < upsertIndex, "the delete must precede the upsert, or the old copy survives");
    }

    [Fact]
    public async Task PromotingTheSameFileTwice_DeletesOncePerPromotion()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");
        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Equal(2, writer.DeletedPaths.Count(p => p == "Editing/Foo.cs"));
    }

    [Fact]
    public async Task PromotingTwoFiles_DeletesEachPathExactlyOnce()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");
        await service.PromoteNowAsync(MakeCachedFile("Editing/Bar.cs", 2), "myrepo");

        Assert.Equal(["Editing/Foo.cs", "Editing/Bar.cs"], writer.DeletedPaths);
    }

    [Fact]
    public async Task WhenTheCollectionIsMissing_NothingIsDeletedAndNothingIsUpserted()
    {
        var writer = new FakeQdrantWriter { CollectionExists = false };
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Empty(writer.DeletedPaths);
        Assert.Equal(0, writer.UpsertedPointCount);
    }
}
