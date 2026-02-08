using System.ComponentModel;
using System.Text.Json;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for repository context management.
/// Controls which repositories are actively watched for file changes.
/// </summary>
[McpServerToolType]
public class RepositoryTools(
    FileWatcherService fileWatcher,
    HotCache hotCache,
    RepositoryIndexer indexer,
    ILogger<RepositoryTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("set_active_repository")]
    [Description("Set the active repository for file watching. Stops watching all other repositories and starts watching the specified one. Use this when switching between projects to ensure only the current project is monitored for changes.")]
    public async Task<string> SetActiveRepository(
        string repositoryName,
        bool clearOtherCaches = true)
    {
        try
        {
            // Resolve repository name to path
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName);
            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Index the repository first using index_repository."
                }, _jsonOptions);
            }

            string targetPath = state.RootPath;
            IReadOnlyList<string> currentlyWatched = fileWatcher.GetWatchedRepositories();
            var stoppedWatching = new List<string>();
            var clearedCaches = new List<string>();

            // Stop watching all other repositories
            foreach (string watchedPath in currentlyWatched)
            {
                if (!string.Equals(watchedPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileWatcher.StopWatching(watchedPath);
                    stoppedWatching.Add(watchedPath);

                    if (clearOtherCaches)
                    {
                        hotCache.ClearRepository(watchedPath);
                        clearedCaches.Add(watchedPath);
                    }
                }
            }

            // Start watching the target repository if not already
            if (!fileWatcher.IsWatching(targetPath))
            {
                fileWatcher.WatchRepository(targetPath);
            }

            logger.LogInformation(
                "Set active repository to {Repository} at {Path}. Stopped watching {StoppedCount} other repositories.",
                repositoryName, targetPath, stoppedWatching.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                activeRepository = repositoryName,
                activePath = targetPath,
                stoppedWatching,
                clearedCaches,
                message = $"Now watching '{repositoryName}' exclusively"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting active repository to {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_watched_repositories")]
    [Description("Get the list of repositories currently being watched for file changes.")]
    public Task<string> GetWatchedRepositories()
    {
        try
        {
            IReadOnlyList<string> watched = fileWatcher.GetWatchedRepositories();
            int hotCacheCount = hotCache.Count;

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                watchedRepositories = watched,
                watchedCount = watched.Count,
                hotCacheFileCount = hotCacheCount
            }, _jsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting watched repositories");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions));
        }
    }

    [McpServerTool, DisplayName("stop_watching_all")]
    [Description("Stop watching all repositories for file changes and clear the hot cache. Use this to free resources when you're done working.")]
    public Task<string> StopWatchingAll(bool clearHotCache = true)
    {
        try
        {
            List<string> watched = fileWatcher.GetWatchedRepositories().ToList();

            foreach (string path in watched)
            {
                fileWatcher.StopWatching(path);
            }

            var cacheCleared = false;
            if (clearHotCache)
            {
                hotCache.Clear();
                cacheCleared = true;
            }

            logger.LogInformation(
                "Stopped watching {Count} repositories. Cache cleared: {CacheCleared}",
                watched.Count, cacheCleared);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                stoppedWatching = watched,
                stoppedCount = watched.Count,
                cacheCleared,
                message = watched.Count > 0
                    ? $"Stopped watching {watched.Count} repositories"
                    : "No repositories were being watched"
            }, _jsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping all watchers");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions));
        }
    }
}
