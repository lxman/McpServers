using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class IndexStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "codeassist-state-" + Guid.NewGuid().ToString("N"));

    private IndexStateStore MakeStore(ILogger<IndexStateStore>? logger = null) =>
        new(Options.Create(new CodeAssistOptions { IndexStateDirectory = _dir }),
            logger ?? NullLogger<IndexStateStore>.Instance);

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
        VectorDimension = 768,
        CollectionName = "myrepo",
        IncludePatterns = ["*.cs"],
        ExcludePatterns = [],
        Files = []
    };

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        IndexStateStore store = MakeStore();

        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddHours(-1)),
            TestContext.Current.CancellationToken);
        IndexStateFile? loaded = await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal("myrepo", loaded.CollectionName);
        Assert.Equal("aaaa111", loaded.LastCommitSha);
        Assert.Equal(768, loaded.VectorDimension);
    }

    [Fact]
    public async Task TouchAsync_AdvancesLastUpdatedAt()
    {
        // This test has failed rarely under full-suite load, and twice the cause was guessed at as
        // timing precision -- which it cannot be, since the comparison is against a timestamp two days
        // old. The only way to lose is for the write not to persist, and TouchAsync swallows every
        // exception into a warning, so on a NullLogger the one piece of evidence went nowhere. The
        // recorder below carries it into the failure message instead. "(no warnings logged)" is itself
        // informative: it rules the write path out and says look somewhere else.
        var log = new RecordingLogger();
        IndexStateStore store = MakeStore(log);
        DateTimeOffset stale = DateTimeOffset.UtcNow.AddDays(-2);
        await store.SaveAsync("MyRepo", MakeState(stale), TestContext.Current.CancellationToken);

        await store.TouchAsync("myrepo", commitSha: null,
            cancellationToken: TestContext.Current.CancellationToken);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.True(loaded.LastUpdatedAt > stale,
            "a promotion is an update and must advance lastUpdated. " + log.WarningReport);
    }

    [Fact]
    public async Task TouchAsync_UpdatesCommitShaWhenGiven()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-2)),
            TestContext.Current.CancellationToken);

        await store.TouchAsync("myrepo", commitSha: "bbbb222",
            cancellationToken: TestContext.Current.CancellationToken);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);
        Assert.Equal("bbbb222", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_LeavesCommitShaAloneWhenNotGiven()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-2)),
            TestContext.Current.CancellationToken);

        await store.TouchAsync("myrepo", commitSha: null,
            cancellationToken: TestContext.Current.CancellationToken);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);
        Assert.Equal("aaaa111", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_IsSilentWhenNoStateFileExists()
    {
        IndexStateStore store = MakeStore();

        await store.TouchAsync("neverindexed", commitSha: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(await store.LoadAsync("neverindexed", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_ThrowsRatherThanReportingAnUnreadableFileAsAbsent()
    {
        // Returning null here would tell the indexer the repository was never indexed, which makes it
        // reclassify every file as new, skip every delete, and duplicate the whole collection.
        IndexStateStore store = MakeStore();
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(store.GetStatePath("MyRepo"), "{ truncated",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.LoadAsync("MyRepo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_RejectsARepositoryNameThatSanitizesToAnExistingName()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("My-Repo", MakeState(DateTimeOffset.UtcNow) with
        {
            RepositoryName = "My-Repo"
        }, TestContext.Current.CancellationToken);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.LoadAsync("My_Repo", TestContext.Current.CancellationToken));

        Assert.Contains("same collection and state file", exception.Message);
    }

    [Fact]
    public async Task Delete_RejectsARepositoryNameThatSanitizesToAnExistingName()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("My-Repo", MakeState(DateTimeOffset.UtcNow) with
        {
            RepositoryName = "My-Repo"
        }, TestContext.Current.CancellationToken);

        Assert.Throws<InvalidOperationException>(() => store.Delete("My_Repo"));
        Assert.True(File.Exists(store.GetStatePath("My-Repo")));
    }

    [Fact]
    public async Task ListRepositoryNamesAsync_ReturnsStoredNamesInsteadOfSanitizedFileNames()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("My-Repo", MakeState(DateTimeOffset.UtcNow) with
        {
            RepositoryName = "My-Repo"
        }, TestContext.Current.CancellationToken);

        List<string> names = await store.ListRepositoryNamesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["My-Repo"], names);
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
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

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
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-1)),
            TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(store.GetStatePath("MyRepo") + ".tmp", "{ this is not valid json",
            TestContext.Current.CancellationToken);

        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal("aaaa111", loaded.LastCommitSha);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_RetriesTheMoveAfterAnotherHandleBlocksIt()
    {
        // The failure this guards: something opens the state file microseconds after it is written --
        // on Windows that routinely means Defender or the search indexer -- and moving the temp file
        // over a destination held without FILE_SHARE_DELETE fails on the spot. SaveAsync does not
        // swallow, and it is the last step of a ~15-minute index run whose chunks are already in
        // Qdrant, so one lock that clears in milliseconds costs a full reindex.
        //
        // The blocking handle is released only once a failed attempt has actually been observed, so
        // this cannot pass by the move happening to be late: without the retry the save has already
        // faulted by the time the handle is let go.
        var log = new RecordingLogger();
        IndexStateStore store = MakeStore(log);
        string path = store.GetStatePath("MyRepo");
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-1)),
            TestContext.Current.CancellationToken);

        FileStream blocker = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Task save = store.SaveAsync("MyRepo",
            MakeState(DateTimeOffset.UtcNow) with { LastCommitSha = "cccc333" },
            TestContext.Current.CancellationToken);
        await Task.WhenAny(log.FirstWarning, save,
            Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        blocker.Dispose();

        await save;

        IndexStateFile? loaded = await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);
        Assert.Equal("cccc333", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task SaveAsync_GivesUpRatherThanRetryingForeverWhenTheHandleIsNeverReleased()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-1)),
            TestContext.Current.CancellationToken);

        using FileStream blocker =
            File.Open(store.GetStatePath("MyRepo"), FileMode.Open, FileAccess.Read, FileShare.Read);

        Task save = store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);
        Task finished = await Task.WhenAny(save,
            Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(save, finished);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => save);
        Assert.True(ex is IOException or UnauthorizedAccessException, ex.ToString());
    }


    [Fact]
    public async Task LoadAsync_NeverBlocksAConcurrentSave()
    {
        // Windows cannot swap a file into a name a reader holds open, so a read racing a write does
        // not merely observe it -- it breaks it. In this process that is a background TouchAsync
        // losing to a get_index_status call, no antivirus required.
        //
        // A single race proves nothing either way, because whether the read is still in flight when
        // the swap happens is a coin toss: unserialised, this loop logged a warning on roughly half
        // of its runs. Repeating it turns that into a near-certainty, and a retried swap always
        // leaves a warning behind, so zero warnings across every round is the assertion.
        var log = new RecordingLogger();
        IndexStateStore store = MakeStore(log);

        // Padding on every write, so the file stays big enough for a read to still be in flight.
        IndexStateFile Padded(string sha) =>
            MakeState(DateTimeOffset.UtcNow) with { LastCommitSha = sha, RootPath = new string('x', 4_000_000) };

        await store.SaveAsync("MyRepo", Padded("aaaa111"), TestContext.Current.CancellationToken);

        for (var round = 0; round < 20; round++)
        {
            Task<IndexStateFile?> read = store.LoadAsync("MyRepo", TestContext.Current.CancellationToken);
            Task save = store.SaveAsync("MyRepo", Padded($"sha{round:0000}"),
                TestContext.Current.CancellationToken);
            await Task.WhenAll(read, save);

            Assert.NotNull(await read);
        }

        Assert.True(log.Warnings.Count == 0, "a concurrent read blocked a write: " + log.WarningReport);
        Assert.Equal("sha0019",
            (await store.LoadAsync("MyRepo", TestContext.Current.CancellationToken))!.LastCommitSha);
    }

    /// <summary>
    /// Captures what the store logs at warning or above. The store swallows in two places, so a
    /// warning is often the only trace a write left behind: <see cref="FirstWarning"/> lets a test
    /// wait for a failed move attempt, and <see cref="Warnings"/> carries the exception text into an
    /// assertion message instead of leaving the cause to be guessed at.
    /// </summary>
    private sealed class RecordingLogger : ILogger<IndexStateStore>
    {
        private readonly TaskCompletionSource _firstWarning = new();
        private readonly List<string> _warnings = [];

        public Task FirstWarning => _firstWarning.Task;

        public IReadOnlyList<string> Warnings
        {
            get { lock (_warnings) return _warnings.ToArray(); }
        }

        public string WarningReport =>
            Warnings.Count == 0 ? "(no warnings logged)" : string.Join(" | ", Warnings);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Warning) return;

            string entry = formatter(state, exception) + (exception is null ? "" : " -> " + exception);
            lock (_warnings) _warnings.Add(entry);
            _firstWarning.TrySetResult();
        }
    }
}
