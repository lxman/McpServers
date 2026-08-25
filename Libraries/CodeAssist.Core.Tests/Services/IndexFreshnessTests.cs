using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class IndexFreshnessTests
{
    [Fact]
    public void ResolveLastFullIndexAt_AdvancesAfterCompleteRefresh()
    {
        DateTimeOffset previous = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset indexedAt = DateTimeOffset.UtcNow;
        IndexStateFile existing = MakeState(previous);

        DateTimeOffset? resolved = RepositoryIndexer.ResolveLastFullIndexAt(
            existing, indexedAt, failedFileCount: 0);

        Assert.Equal(indexedAt, resolved);
    }

    [Fact]
    public void ResolveLastFullIndexAt_PreservesPreviousStampAfterPartialRefresh()
    {
        DateTimeOffset previous = DateTimeOffset.UtcNow.AddDays(-1);
        IndexStateFile existing = MakeState(previous);

        DateTimeOffset? resolved = RepositoryIndexer.ResolveLastFullIndexAt(
            existing, DateTimeOffset.UtcNow, failedFileCount: 1);

        Assert.Equal(previous, resolved);
    }

    [Fact]
    public void ResolveLastFullIndexAt_RemainsNullWhenInitialIndexIsPartial()
    {
        Assert.Null(RepositoryIndexer.ResolveLastFullIndexAt(
            existingState: null, DateTimeOffset.UtcNow, failedFileCount: 1));
    }

    [Fact]
    public void CreateIndexedFileState_RecordsSuccessfullyProcessedZeroChunkFile()
    {
        IndexedFile state = RepositoryIndexer.CreateIndexedFileState(
            "empty.cs", "", DateTime.UtcNow, []);

        Assert.Equal("empty.cs", state.RelativePath);
        Assert.Equal(0, state.ChunkCount);
        Assert.Empty(state.ChunkIds);
        Assert.NotEmpty(state.ContentHash);
    }

    private static IndexStateFile MakeState(DateTimeOffset lastFullIndexAt) => new()
    {
        RepositoryName = "repo",
        RootPath = "C:/repo",
        LastCommitSha = "abc",
        CreatedAt = lastFullIndexAt,
        LastUpdatedAt = lastFullIndexAt,
        LastFullIndexAt = lastFullIndexAt,
        EmbeddingModel = "model",
        CollectionName = "repo",
        IncludePatterns = ["*.cs"],
        ExcludePatterns = [],
        Files = []
    };
}
