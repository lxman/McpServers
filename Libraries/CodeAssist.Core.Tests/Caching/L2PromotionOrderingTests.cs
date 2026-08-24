using CodeAssist.Core.Caching;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class L2PromotionOrderingTests : IDisposable
{
    // Promotion now checks File.Exists on the cached file's path before writing, so these fixtures
    // write real files under a temp root rather than pointing at a path like C:\repo\... that was
    // never on disk — a fake path would make every promotion look like a deleted file and be skipped.
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "codeassist-l2order-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private CachedFile MakeCachedFile(string relativePath, int chunkCount)
    {
        string fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "content");

        var chunks = new List<CodeChunk>();
        var embeddings = new List<float[]>();
        for (var i = 0; i < chunkCount; i++)
        {
            chunks.Add(new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = fullPath,
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
            FilePath = fullPath,
            RelativePath = relativePath,
            RepositoryRoot = _root,
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
            new IndexStateStore(
                Options.Create(new CodeAssistOptions
                {
                    IndexStateDirectory = Path.Combine(
                        Path.GetTempPath(), "codeassist-test-state-" + Guid.NewGuid().ToString("N"))
                }),
                NullLogger<IndexStateStore>.Instance),
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

    [Fact]
    public async Task TwoSavesOfOneFileInOneBatch_WriteOnlyTheLatestChunks()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        CachedFile first = MakeCachedFile("Editing/Foo.cs", 2);
        CachedFile second = MakeCachedFile("Editing/Foo.cs", 3);

        await service.PromoteNowAsync([first, second], "myrepo");

        // One delete, and only the newer chunk set written. Writing both would leave the older
        // version beside the newer one with no way to tell them apart — the reported bug, in miniature.
        Assert.Equal(["Editing/Foo.cs"], writer.DeletedPaths);
        Assert.Equal(3, writer.UpsertedPointCount);
    }

    [Fact]
    public async Task PromotingAFile_SkipsItWhenTheFileWasDeletedSinceBeingQueued()
    {
        // Chunking and embedding are network-bound and take seconds, which is time enough for a save to
        // be queued and then the file to be deleted before the promotion drains. Without a liveness
        // check here the drained promotion would resurrect the deleted file's chunks with no newer copy
        // to outrank them — a stale hit that survives until a full reindex, worse than a duplicate.
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        CachedFile cachedFile = MakeCachedFile("Editing/Foo.cs", 2);
        File.Delete(cachedFile.FilePath);

        await service.PromoteNowAsync(cachedFile, "myrepo");

        Assert.Empty(writer.DeletedPaths);
        Assert.Equal(0, writer.UpsertedPointCount);
    }

    [Fact]
    public async Task PromotingTwoFiles_OneDeletedSinceQueued_StillPromotesTheOtherOne()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        CachedFile stillThere = MakeCachedFile("Editing/Foo.cs", 2);
        CachedFile deleted = MakeCachedFile("Editing/Bar.cs", 2);
        File.Delete(deleted.FilePath);

        await service.PromoteNowAsync([stillThere, deleted], "myrepo");

        Assert.Equal(["Editing/Foo.cs"], writer.DeletedPaths);
        Assert.Equal(2, writer.UpsertedPointCount);
    }
}
