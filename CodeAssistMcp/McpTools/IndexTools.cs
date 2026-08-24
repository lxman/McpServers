using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for repository indexing operations.
/// </summary>
[McpServerToolType]
public class IndexTools(
    RepositoryIndexer indexer,
    FileWatcherService fileWatcher,
    L2PromotionService l2Promotion,
    ILogger<IndexTools> logger)
{
    [McpServerTool, DisplayName("index_repository")]
    [Description("Index a code repository for semantic search. This scans all supported source files, chunks them intelligently (by class/method for C#), generates embeddings, and stores them in the vector database. Supports incremental updates - only changed files are re-indexed.")]
    public async Task<string> IndexRepository(
        string repositoryPath,
        string? repositoryName = null,
        string? includePatterns = null,
        string? excludePatterns = null)
    {
        try
        {
            logger.LogInformation("Indexing repository at {Path}", repositoryPath);

            List<string>? includes = string.IsNullOrEmpty(includePatterns)
                ? null
                : includePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            List<string>? excludes = string.IsNullOrEmpty(excludePatterns)
                ? null
                : excludePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            IndexingResult result = await indexer.IndexRepositoryAsync(
                repositoryPath,
                repositoryName,
                includes,
                excludes);

            // Indexing a repository implies wanting it kept current, so start watching here rather
            // than waiting for a search to do it lazily. Watch state lives in this process and does
            // not survive a restart, so before this the window between "server restarted" and "someone
            // happened to search" was untracked: edits in it never reached the L1 cache at all, and an
            // index that had just been built started going stale immediately. Armed AFTER the index
            // completes, so the run does not race its own file events. Both calls are idempotent.
            if (result.Success)
            {
                string resolvedName = repositoryName ?? Path.GetFileName(repositoryPath.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string fullPath = Path.GetFullPath(repositoryPath);

                if (!fileWatcher.IsWatching(fullPath)) fileWatcher.WatchRepository(fullPath);
                l2Promotion.RegisterRepositoryCollection(
                    fullPath, CollectionNaming.ForRepository(resolvedName));
            }

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                repositoryName = repositoryName ?? Path.GetFileName(repositoryPath),
                filesProcessed = result.FilesProcessed,
                filesAdded = result.FilesAdded,
                filesUpdated = result.FilesUpdated,
                filesRemoved = result.FilesRemoved,
                filesSkipped = result.FilesSkipped,
                totalChunks = result.TotalChunks,
                duration = result.Duration.ToString(),
                failedFiles = result.FailedFiles,
                error = result.ErrorMessage
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error indexing repository at {Path}", repositoryPath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_indexes")]
    [Description("List all indexed repositories with their metadata.")]
    public async Task<string> ListIndexes()
    {
        try
        {
            List<string?> repositories = await indexer.ListIndexedRepositoriesAsync();
            var indexes = new List<object>();
            var unreadable = new List<object>();

            foreach (string? repo in repositories)
            {
                if (string.IsNullOrEmpty(repo))
                    continue;

                IndexState? state;
                try
                {
                    state = await indexer.GetIndexStateAsync(repo);
                }
                catch (Exception ex)
                {
                    // One unreadable state file must not hide every healthy index. Report this
                    // repository as broken and keep listing the rest — the whole point of failing
                    // loudly on a corrupt file is to make it visible, not to make discovery
                    // impossible.
                    logger.LogError(ex, "Failed to read index state for {Repository}", repo);
                    unreadable.Add(new { repositoryName = repo, error = ex.Message });
                    continue;
                }

                if (state != null)
                {
                    indexes.Add(new
                    {
                        repositoryName = state.RepositoryName,
                        rootPath = state.RootPath,
                        fileCount = state.FileCount,
                        chunkCount = state.ChunkCount,
                        lastUpdated = state.LastUpdatedAt,
                        embeddingModel = state.EmbeddingModel,
                        lastCommitSha = state.LastCommitSha
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = indexes.Count,
                indexes,
                unreadableCount = unreadable.Count,
                unreadable
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing indexes");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_index_status")]
    [Description("Get detailed status of a specific repository index.")]
    public async Task<string> GetIndexStatus(string repositoryName)
    {
        try
        {
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName);

            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'"
                }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName = state.RepositoryName,
                rootPath = state.RootPath,
                fileCount = state.FileCount,
                chunkCount = state.ChunkCount,
                createdAt = state.CreatedAt,
                lastUpdatedAt = state.LastUpdatedAt,
                embeddingModel = state.EmbeddingModel,
                collectionName = state.CollectionName,
                lastCommitSha = state.LastCommitSha,
                includePatterns = state.IncludePatterns,
                excludePatterns = state.ExcludePatterns
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting index status for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("delete_index")]
    [Description("Delete a repository index and all its data from the vector database.")]
    public async Task<string> DeleteIndex(string repositoryName)
    {
        try
        {
            logger.LogInformation("Deleting index for repository {Repository}", repositoryName);

            await indexer.DeleteIndexAsync(repositoryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Index for '{repositoryName}' deleted successfully"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting index for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("refresh_index")]
    [Description("Refresh an existing repository index by detecting and processing changed files. This is faster than a full re-index as it only processes files that have changed since the last index.")]
    public async Task<string> RefreshIndex(string repositoryName)
    {
        try
        {
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName);

            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Refreshing index for repository {Repository} at {Path}",
                repositoryName, state.RootPath);

            IndexingResult result = await indexer.IndexRepositoryAsync(
                state.RootPath,
                repositoryName,
                state.IncludePatterns,
                state.ExcludePatterns);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                repositoryName,
                filesProcessed = result.FilesProcessed,
                filesAdded = result.FilesAdded,
                filesUpdated = result.FilesUpdated,
                filesRemoved = result.FilesRemoved,
                filesSkipped = result.FilesSkipped,
                totalChunks = result.TotalChunks,
                duration = result.Duration.ToString(),
                error = result.ErrorMessage
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing index for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
