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
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CodeAssistOptions _options = options.Value;

    public string GetStatePath(string repositoryName) =>
        Path.Combine(_options.IndexStateDirectory, $"{CollectionNaming.ForRepository(repositoryName)}.json");

    public async Task<IndexStateFile?> LoadAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);
        if (!File.Exists(path)) return null;

        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<IndexStateFile>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read index state at {Path}", path);
            return null;
        }
    }

    public async Task SaveAsync(string repositoryName, IndexStateFile state, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(state, SerializerOptions), cancellationToken);
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

            IndexStateFile updated = state with
            {
                LastUpdatedAt = DateTimeOffset.UtcNow,
                LastCommitSha = commitSha ?? state.LastCommitSha
            };

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(updated, SerializerOptions), cancellationToken);
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

    public void Delete(string repositoryName)
    {
        string path = GetStatePath(repositoryName);
        if (File.Exists(path)) File.Delete(path);
    }
}
