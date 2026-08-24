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
    public async Task PromotingAFile_WritesNewChunksBeforeRemovingOldOnes()
    {
        var writer = new FakeQdrantWriter();
        // The fake now mirrors production and skips the "deleteIds:" log entry entirely for an empty
        // id list, so at least one prior point must be seeded or there is nothing here to prove the
        // ordering of.
        writer.ExistingPointIds["Editing/Foo.cs"] = [Guid.NewGuid()];
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 3), "myrepo");

        int upsertIndex = writer.Calls.FindIndex(c => c.StartsWith("upsert:", StringComparison.Ordinal));
        int deleteIdsIndex = writer.Calls.FindIndex(c => c.StartsWith("deleteIds:", StringComparison.Ordinal));

        Assert.True(upsertIndex >= 0, "the new chunks must be upserted");
        Assert.True(deleteIdsIndex >= 0, "the file's prior chunks must be removed by id");
        // The write must land before the old generation is removed: a failed upsert must leave the
        // file's prior chunks in place — stale, still searchable under old content — rather than
        // gone. Deleting first (the old order) turned a failed write into an absent file, which is
        // worse than stale.
        Assert.True(upsertIndex < deleteIdsIndex, "the upsert must precede the id delete, or a failed write leaves the file absent");
    }

    [Fact]
    public async Task PromotingAFile_RemovesExactlyThePointsItSuperseded()
    {
        var writer = new FakeQdrantWriter();
        List<Guid> oldIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        writer.ExistingPointIds["Editing/Foo.cs"] = oldIds;
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        CachedFile cachedFile = MakeCachedFile("Editing/Foo.cs", 3);
        List<Guid> newIds = cachedFile.Chunks.Select(c => c.Id).ToList();

        await service.PromoteNowAsync(cachedFile, "myrepo");

        Assert.Equal(oldIds, writer.DeletedIds);
        Assert.DoesNotContain(writer.DeletedIds, id => newIds.Contains(id));
    }

    [Fact]
    public async Task PromotingTwoFiles_RemovesBothFilesOldPointsInOneCall()
    {
        // The accumulation the reorder introduced: every file's superseded ids collapse into a single
        // delete for the batch. Nothing else covers this with real ids — the sibling two-file test
        // leaves both id sets empty, so AddRange never runs with anything in it.
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        var fooOld = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var barOld = new[] { Guid.NewGuid() };
        writer.ExistingPointIds["Editing/Foo.cs"] = fooOld.ToList();
        writer.ExistingPointIds["Editing/Bar.cs"] = barOld.ToList();

        await service.PromoteNowAsync(
            [MakeCachedFile("Editing/Foo.cs", 2), MakeCachedFile("Editing/Bar.cs", 2)], "myrepo");

        Assert.Equal(1, writer.Calls.Count(c => c.StartsWith("deleteIds:", StringComparison.Ordinal)));
        Assert.Equal(fooOld.Concat(barOld).OrderBy(g => g), writer.DeletedIds.OrderBy(g => g));
    }

    [Fact]
    public async Task WhenTheUpsertFails_NothingIsDeleted()
    {
        // The whole point of writing before deleting: a failed write must leave the old generation
        // untouched so the file stays searchable under its previous content.
        var writer = new FakeQdrantWriter { ThrowOnUpsert = true };
        writer.ExistingPointIds["Editing/Foo.cs"] = [Guid.NewGuid(), Guid.NewGuid()];
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Empty(writer.DeletedIds);
    }

    [Fact]
    public async Task WhenRemovingTheOldPointsFails_ThePromotionStillCounts()
    {
        // The write succeeded; only the cleanup of the superseded generation failed. That is not a
        // lost promotion — it self-repairs on the next successful promotion of the same file — so it
        // must not inflate DroppedPromotionCount.
        var writer = new FakeQdrantWriter { ThrowOnDeleteIds = true };
        writer.ExistingPointIds["Editing/Foo.cs"] = [Guid.NewGuid()];
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Equal(2, writer.UpsertedPointCount);
        Assert.Equal(0, service.DroppedPromotionCount);
    }

    [Fact]
    public async Task WhenTheOldPointIdsCannotBeRead_TheFileIsSkippedAndNothingIsDeleted()
    {
        // If the old generation's ids can't be learned, the file can't be safely superseded: writing
        // new chunks anyway would just add a duplicate copy alongside chunks nobody can find again to
        // remove. So the file is excluded from the batch entirely, and its old chunks stay in place.
        var writer = new FakeQdrantWriter { ThrowOnGetIds = true };
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Equal(0, writer.UpsertedPointCount);
        Assert.Empty(writer.DeletedIds);
    }

    [Fact]
    public async Task PromotingTheSameFileTwice_DeletesOncePerPromotion()
    {
        // Superseding a file's chunks now goes through an id fetch + id delete rather than a
        // path delete (DeletedPaths is only ever populated by RemoveFileAsync now), so "once per
        // promotion" is asserted against the "ids:" lookup, which fires exactly once per file per
        // batch just as the old path delete did.
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");
        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Equal(2, writer.Calls.Count(c => c == "ids:Editing/Foo.cs"));
    }

    [Fact]
    public async Task PromotingTwoFiles_DeletesEachPathExactlyOnce()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");
        await service.PromoteNowAsync(MakeCachedFile("Editing/Bar.cs", 2), "myrepo");

        Assert.Equal(
            ["ids:Editing/Foo.cs", "ids:Editing/Bar.cs"],
            writer.Calls.Where(c => c.StartsWith("ids:", StringComparison.Ordinal)).ToList());
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

        // One id fetch, and only the newer chunk set written. Writing both would leave the older
        // version beside the newer one with no way to tell them apart — the reported bug, in miniature.
        Assert.Equal(
            ["ids:Editing/Foo.cs"],
            writer.Calls.Where(c => c.StartsWith("ids:", StringComparison.Ordinal)).ToList());
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

        Assert.Equal(
            ["ids:Editing/Foo.cs"],
            writer.Calls.Where(c => c.StartsWith("ids:", StringComparison.Ordinal)).ToList());
        Assert.Equal(2, writer.UpsertedPointCount);
    }
}
