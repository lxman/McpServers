using System.Text.Json;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Services;

/// <summary>
/// Reads and writes the per-repository index state file.
/// </summary>
/// <remarks>
/// The state file's name is <c>CollectionNaming.ForRepository(repositoryName) + ".json"</c>, which is the
/// same string as the Qdrant collection name — that is what lets the promotion path find the right state
/// file knowing only the collection it just wrote to.
///
/// <para><c>lastUpdated</c> and <c>lastCommitSha</c> previously advanced only on a manual refresh, so they
/// meant "last manual refresh" while being read as "last update". A repository could report a stamp two
/// days and many commits stale while its index held files written that morning.</para>
/// </remarks>
public sealed class IndexStateStore(
    IOptions<CodeAssistOptions> options,
    ILogger<IndexStateStore> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    // Backoff between attempts at the move below. This is insurance against a holder outside this
    // process, which nothing here has actually demonstrated -- the in-process reader that did cause
    // the failure is handled by LoadAsync taking the write lock, not by retrying. A short ladder
    // clears a transient holder; anything still holding after roughly a second is not transient.
    private static readonly int[] MoveRetryDelaysMs = [20, 50, 100, 200, 400];
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CodeAssistOptions _options = options.Value;

    public string GetStatePath(string repositoryName) =>
        Path.Combine(_options.IndexStateDirectory, $"{CollectionNaming.ForRepository(repositoryName)}.json");

    public async Task<IndexStateFile?> LoadAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);

        // Reads take the write lock too. Windows will not move a file onto a name that a reader
        // holds open: measured over twenty rounds, a concurrent read broke the write every single
        // time, and the same loop with no reader broke none. A lock-free read here is therefore not a
        // passive observer, it is the thing that fails the write -- in this process, a background
        // TouchAsync losing to a get_index_status call, with nothing external involved. Serialising
        // costs a status call the tail of an in-flight write, which is milliseconds.
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path)) return null;

            string json = await File.ReadAllTextAsync(path, cancellationToken);
            IndexStateFile? state = JsonSerializer.Deserialize<IndexStateFile>(json);
            ValidateRepositoryName(repositoryName, state);
            return state;
        }
        catch (Exception ex)
        {
            // Deliberately not swallowed. "Absent" and "unreadable" must not look alike here: a caller
            // that reads a corrupt state file as "never indexed" reclassifies every file as new, skips
            // every delete, and writes a second complete copy of the collection while reporting
            // success. Failing the operation is recoverable; silently duplicating it is not.
            logger.LogError(ex, "Index state at {Path} exists but could not be read", path);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<string>> ListRepositoryNamesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_options.IndexStateDirectory)) return [];

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var names = new List<string>();
            foreach (string path in Directory.GetFiles(_options.IndexStateDirectory, "*.json"))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path, cancellationToken);
                    IndexStateFile? state = JsonSerializer.Deserialize<IndexStateFile>(json);
                    names.Add(state?.RepositoryName ?? Path.GetFileNameWithoutExtension(path));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Keep corrupt files visible to list_indexes, which will attempt to load this
                    // fallback name and report the detailed read error without hiding healthy indexes.
                    logger.LogError(ex, "Could not read repository identity from {Path}", path);
                    names.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            return names;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveAsync(string repositoryName, IndexStateFile state, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        ValidateRepositoryName(repositoryName, state);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                string existingJson = await File.ReadAllTextAsync(path, cancellationToken);
                IndexStateFile? existingState = JsonSerializer.Deserialize<IndexStateFile>(existingJson);
                ValidateRepositoryName(repositoryName, existingState);
            }

            await WriteAtomicAsync(path, JsonSerializer.Serialize(state, SerializerOptions), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Advance the freshness stamps for an already-indexed repository, identified by its collection name.
    /// Does nothing if the repository has no state file — a promotion is not an index.
    /// </summary>
    public async Task TouchAsync(string collectionName, string? commitSha, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            string path = Path.Combine(_options.IndexStateDirectory, $"{collectionName}.json");
            if (!File.Exists(path)) return;

            string json = await File.ReadAllTextAsync(path, cancellationToken);
            IndexStateFile? state = JsonSerializer.Deserialize<IndexStateFile>(json);
            if (state is null) return;

            DateTimeOffset promotedAt = DateTimeOffset.UtcNow;
            IndexStateFile updated = state with
            {
                LastUpdatedAt = promotedAt,
                LastPromotionAt = promotedAt,
                LastCommitSha = commitSha ?? state.LastCommitSha
            };

            await WriteAtomicAsync(path, JsonSerializer.Serialize(updated, SerializerOptions), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to touch index state for collection {Collection}", collectionName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Write the file so a reader sees either the whole previous version or the whole new one.
    /// </summary>
    /// <remarks>
    /// <see cref="File.WriteAllTextAsync(string, string?, CancellationToken)"/> truncates before it
    /// writes, so anything that interrupts it — a crash, a kill, a full disk — leaves a permanently
    /// truncated state file rather than a briefly inconsistent one. These files run to megabytes, so
    /// that window is not theoretical, and a lost state file costs a full reindex. Writing to a
    /// sibling temp file and moving it into place makes the swap atomic on the same volume.
    ///
    /// <para>The move cannot survive a reader holding the destination — measured over twenty rounds it
    /// failed every time against one, under every share mode, and <see cref="File.Replace(string,string,string)"/>
    /// is worse rather than better, failing roughly a third of the time entirely unopposed. That is
    /// why <see cref="LoadAsync"/> takes the write lock instead of relying on share flags.</para>
    ///
    /// <para>The retry below is for a holder outside this process, which is plausible but was never
    /// observed here. It matters because <see cref="SaveAsync"/> does not swallow and is the last step
    /// of an index run whose chunks are already in Qdrant, so a lock clearing in milliseconds would
    /// otherwise discard fifteen minutes of work. Note the failure is
    /// <see cref="UnauthorizedAccessException"/>, not the <see cref="IOException"/> the name suggests.
    /// Exhausting the attempts still throws — that is a real failure, and <see cref="TouchAsync"/>'s
    /// catch remains the last resort for the stamp it can afford to lose.</para>
    /// </remarks>
    private async Task WriteAtomicAsync(string path, string contents, CancellationToken cancellationToken)
    {
        string tempPath = path + ".tmp";

        await File.WriteAllTextAsync(tempPath, contents, cancellationToken);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(tempPath, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       && attempt < MoveRetryDelaysMs.Length)
            {
                logger.LogWarning(ex,
                    "Could not move index state into place at {Path} (attempt {Attempt} of {Total}); "
                    + "retrying in {Delay}ms",
                    path, attempt + 1, MoveRetryDelaysMs.Length + 1, MoveRetryDelaysMs[attempt]);

                await Task.Delay(MoveRetryDelaysMs[attempt], cancellationToken);
            }
        }
    }

    /// <summary>
    /// Remove a repository's state file.
    /// </summary>
    /// <remarks>
    /// Takes the write lock for the same reason <see cref="LoadAsync"/> does: this method reads the
    /// file before deleting it, and on Windows a reader holding the path open is enough to fail a
    /// concurrent <see cref="WriteAtomicAsync"/> move.
    /// </remarks>
    public async Task DeleteAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path)) return;

            IndexStateFile? state = JsonSerializer.Deserialize<IndexStateFile>(
                await File.ReadAllTextAsync(path, cancellationToken));

            ValidateRepositoryName(repositoryName, state);
            File.Delete(path);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the caller's decision, not a delete failure. Swallowing it here
            // would report a cancelled delete as an unlucky one, with only a log line to see.
            // ListRepositoryNamesAsync already rethrows it ahead of its generic catch.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete index state at {Path}", path);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal static void ValidateRepositoryName(string requestedName, IndexStateFile? state)
    {
        if (state is null || string.Equals(
                requestedName, state.RepositoryName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Repository name '{requestedName}' maps to the same collection and state file as "
            + $"existing repository '{state.RepositoryName}'. Choose a distinct repository name.");
    }
}
