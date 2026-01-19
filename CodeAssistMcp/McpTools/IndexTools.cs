using System.ComponentModel;
using System.Text.Json;
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
    ILogger<IndexTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

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

            var includes = string.IsNullOrEmpty(includePatterns)
                ? null
                : includePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var excludes = string.IsNullOrEmpty(excludePatterns)
                ? null
                : excludePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var result = await indexer.IndexRepositoryAsync(
                repositoryPath,
                repositoryName,
                includes,
                excludes);

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
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error indexing repository at {Path}", repositoryPath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_indexes")]
    [Description("List all indexed repositories with their metadata.")]
    public async Task<string> ListIndexes()
    {
        try
        {
            var repositories = await indexer.ListIndexedRepositoriesAsync();
            var indexes = new List<object>();

            foreach (var repo in repositories)
            {
                var state = await indexer.GetIndexStateAsync(repo);
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
                indexes
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing indexes");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_index_status")]
    [Description("Get detailed status of a specific repository index.")]
    public async Task<string> GetIndexStatus(string repositoryName)
    {
        try
        {
            var state = await indexer.GetIndexStateAsync(repositoryName);

            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'"
                }, _jsonOptions);
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
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting index status for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
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
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting index for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("refresh_index")]
    [Description("Refresh an existing repository index by detecting and processing changed files. This is faster than a full re-index as it only processes files that have changed since the last index.")]
    public async Task<string> RefreshIndex(string repositoryName)
    {
        try
        {
            var state = await indexer.GetIndexStateAsync(repositoryName);

            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
                }, _jsonOptions);
            }

            logger.LogInformation("Refreshing index for repository {Repository} at {Path}",
                repositoryName, state.RootPath);

            var result = await indexer.IndexRepositoryAsync(
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
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing index for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
