using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class IndexStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "codeassist-state-" + Guid.NewGuid().ToString("N"));

    private IndexStateStore MakeStore() =>
        new(Options.Create(new CodeAssistOptions { IndexStateDirectory = _dir }),
            NullLogger<IndexStateStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static IndexStateFile MakeState(DateTimeOffset lastUpdated) => new()
    {
        RepositoryName = "MyRepo",
        RootPath = @"C:\repo",
        LastCommitSha = "aaaa111",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
        LastUpdatedAt = lastUpdated,
        EmbeddingModel = "bge-base-en-v1.5",
        CollectionName = "myrepo",
        IncludePatterns = ["*.cs"],
        ExcludePatterns = [],
        Files = []
    };

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        IndexStateStore store = MakeStore();

        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddHours(-1)));
        IndexStateFile? loaded = await store.LoadAsync("MyRepo");

        Assert.NotNull(loaded);
        Assert.Equal("myrepo", loaded.CollectionName);
        Assert.Equal("aaaa111", loaded.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_AdvancesLastUpdatedAt()
    {
        IndexStateStore store = MakeStore();
        DateTimeOffset stale = DateTimeOffset.UtcNow.AddDays(-2);
        await store.SaveAsync("MyRepo", MakeState(stale));

        await store.TouchAsync("myrepo", commitSha: null);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.NotNull(loaded);
        Assert.True(loaded.LastUpdatedAt > stale, "a promotion is an update and must advance lastUpdated");
    }

    [Fact]
    public async Task TouchAsync_UpdatesCommitShaWhenGiven()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-2)));

        await store.TouchAsync("myrepo", commitSha: "bbbb222");

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.Equal("bbbb222", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_LeavesCommitShaAloneWhenNotGiven()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-2)));

        await store.TouchAsync("myrepo", commitSha: null);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.Equal("aaaa111", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_IsSilentWhenNoStateFileExists()
    {
        IndexStateStore store = MakeStore();

        await store.TouchAsync("neverindexed", commitSha: null);

        Assert.Null(await store.LoadAsync("neverindexed"));
    }

    [Fact]
    public async Task LoadAsync_ThrowsRatherThanReportingAnUnreadableFileAsAbsent()
    {
        // Returning null here would tell the indexer the repository was never indexed, which makes it
        // reclassify every file as new, skip every delete, and duplicate the whole collection.
        IndexStateStore store = MakeStore();
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(store.GetStatePath("MyRepo"), "{ truncated");

        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadAsync("MyRepo"));
    }

    [Fact]
    public void Delete_DoesNotThrowWhenTheStateFileIsAbsent()
    {
        IndexStateStore store = MakeStore();

        store.Delete("neverindexed");
    }

    [Fact]
    public async Task Delete_SwallowsTheFailureWhenTheFileCannotBeDeleted()
    {
        // The real scenario the try/catch exists for: something else holds the file open, so
        // File.Delete throws. Before the guard this escaped into a caller with no catch. An absent
        // file never exercised this — the old code's File.Exists check already short-circuited that.
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow));

        using (File.Open(store.GetStatePath("MyRepo"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            store.Delete("MyRepo");
        }
    }

    [Fact]
    public async Task SaveAsync_RecoversFromATemporaryFileLeftByAnEarlierCrash()
    {
        // A process killed between the temp write and the move leaves a stale .tmp behind. The next
        // save has to reclaim it rather than trip over it, or one crash poisons every later write.
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-1)));

        await File.WriteAllTextAsync(store.GetStatePath("MyRepo") + ".tmp", "{ this is not valid json");

        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow));

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.NotNull(loaded);
        Assert.Equal("aaaa111", loaded.LastCommitSha);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }
}
