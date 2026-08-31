using System.Reflection;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public sealed class IndexStateStoreDeleteTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "codeassist-delete-" + Guid.NewGuid().ToString("N"));

    private IndexStateStore MakeStore(ILogger<IndexStateStore>? logger = null) => new(
        Options.Create(new CodeAssistOptions { IndexStateDirectory = _directory }),
        logger ?? NullLogger<IndexStateStore>.Instance);

    // IndexStateFile has nine required members; all of them must be set for this to compile.
    private static IndexStateFile StateFor(string repository) => new()
    {
        RepositoryName = repository,
        RootPath = @"C:\repo",
        CreatedAt = DateTimeOffset.UtcNow,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        EmbeddingModel = "test-model",
        CollectionName = CollectionNaming.ForRepository(repository),
        IncludePatterns = ["**/*.cs"],
        ExcludePatterns = [],
        Files = []
    };

    [Fact]
    public async Task DeleteAsync_RemovesTheStateFile()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo"), TestContext.Current.CancellationToken);

        await store.DeleteAsync("Repo", TestContext.Current.CancellationToken);

        Assert.Null(await store.LoadAsync("Repo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_StillRejectsAMismatchedRepositoryName()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo"), TestContext.Current.CancellationToken);

        // Rewrite the file's own idea of which repository it belongs to. Deleting by the path's
        // name must refuse rather than destroy another repository's state.
        string path = store.GetStatePath("Repo");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("\"RepositoryName\": \"Repo\"", "\"RepositoryName\": \"Different\""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.DeleteAsync("Repo", TestContext.Current.CancellationToken));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotBreakAConcurrentSave()
    {
        // Borrows both mechanisms from the sibling LoadAsync_NeverBlocksAConcurrentSave, which is
        // the proven technique for this exact race: padding so a read is still in flight when a
        // concurrent move lands, and a RecordingLogger so a single blocked-move warning fails the
        // test. The first attempt at this test (Task 10 round 1) used a tiny, near-instantly-read
        // state file and only failed if SaveAsync threw -- which requires exhausting the entire
        // [20, 50, 100, 200, 400]ms retry ladder, not just one collision. That gave the race almost
        // no window and a detector that only trips on total retry exhaustion, and it never went red
        // under mutation even after escalating rounds and padding far past what was specified.
        // Neither escalation could have helped: they didn't touch either root cause.
        var log = new RecordingLogger();
        IndexStateStore store = MakeStore(log);

        IndexStateFile Padded(string sha) =>
            StateFor("MyRepo") with { LastCommitSha = sha, RootPath = new string('x', 4_000_000) };

        await store.SaveAsync("MyRepo", Padded("aaaa111"), TestContext.Current.CancellationToken);

        for (var round = 0; round < 20; round++)
        {
            Task deleting = store.DeleteAsync("MyRepo", TestContext.Current.CancellationToken);
            Task saving = store.SaveAsync("MyRepo", Padded($"sha{round:0000}"),
                TestContext.Current.CancellationToken);
            await Task.WhenAll(deleting, saving);
        }

        Assert.True(log.Warnings.Count == 0, "a concurrent delete blocked a write: " + log.WarningReport);
    }

    [Fact]
    public async Task DeleteAsync_AndSaveAsync_ShareTheSameWriteLock()
    {
        // The concurrency test above proves DeleteAsync no longer breaks a racing SaveAsync, but
        // that is a timing-dependent proof. This test proves the mechanism directly: reflect into
        // the private _writeLock field, hold that exact semaphore instance externally, and show
        // both a DeleteAsync and a SaveAsync call are left pending on it. Asserting only that
        // DeleteAsync is blocked would prove no more than "it respects some field called
        // _writeLock" -- asserting both are pending on the one instance we are holding proves they
        // contend on the SAME lock SaveAsync does, which is the actual mutual-exclusion claim.
        //
        // Deliberately coupled to the private field's name and type (SemaphoreSlim): if the
        // locking strategy is ever refactored, this test breaks and must be updated alongside it.
        // That coupling is the trade for a deterministic proof of a private invariant.
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", StateFor("MyRepo"), TestContext.Current.CancellationToken);

        var writeLock = (SemaphoreSlim)(typeof(IndexStateStore)
                .GetField("_writeLock", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(store)
            ?? throw new InvalidOperationException("IndexStateStore._writeLock not found."));

        await writeLock.WaitAsync(TestContext.Current.CancellationToken);

        Task deleteTask;
        Task saveTask;
        try
        {
            deleteTask = store.DeleteAsync("MyRepo", TestContext.Current.CancellationToken);
            saveTask = store.SaveAsync("MyRepo", StateFor("MyRepo"), TestContext.Current.CancellationToken);

            // Give both tasks a real chance to reach their own WaitAsync call before checking that
            // they are blocked -- an immediate check could observe them before either has run at
            // all, which would prove nothing.
            await Task.Delay(200, TestContext.Current.CancellationToken);

            Assert.False(deleteTask.IsCompleted,
                "DeleteAsync completed while an external holder had the write lock.");
            Assert.False(saveTask.IsCompleted,
                "SaveAsync completed while an external holder had the write lock.");
        }
        finally
        {
            // Release even if an assertion above just threw, so a failed check reports cleanly
            // instead of wedging the two pending tasks (and the test run) forever.
            writeLock.Release();
        }

        // A genuine regression here should fail fast, not hang the suite.
        await Task.WhenAll(deleteTask, saveTask)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_PropagatesCancellationInsteadOfReportingItAsAFailure()
    {
        // A caller that cancels should see OperationCanceledException, not a quietly-completed
        // Task with a warning logged nobody is listening for. The state file is padded so the
        // internal read is still in flight when we cancel immediately after starting the call --
        // the same "start it, then act before the caller can be scheduled again" idiom used by the
        // lock test above, here applied to force cancellation to land mid-read rather than before
        // the method starts (which the write lock, taken outside the try/catch, already handles
        // correctly today) or after it finishes (which would prove nothing).
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo") with { RootPath = new string('x', 4_000_000) },
            TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        Task deleteTask = store.DeleteAsync("Repo", cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deleteTask);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }

    /// <summary>
    /// Captures what the store logs at warning or above. Copied from the sibling
    /// IndexStateStoreTests.RecordingLogger (that one is private to its own class) rather than
    /// shared, to keep this file's race-detection self-contained.
    /// </summary>
    private sealed class RecordingLogger : ILogger<IndexStateStore>
    {
        private readonly List<string> _warnings = [];

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
        }
    }
}
