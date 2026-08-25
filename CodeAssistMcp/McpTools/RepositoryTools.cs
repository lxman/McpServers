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
/// MCP tools for repository context management.
/// Controls which repositories are actively watched for file changes.
/// </summary>
[McpServerToolType]
public class RepositoryTools(
    FileWatcherService fileWatcher,
    HotCache hotCache,
    RepositoryIndexer indexer,
    L2PromotionService l2Promotion,
    ActiveRepositoryStore activeRepositoryStore,
    ILogger<RepositoryTools> logger)
{
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
                }, SerializerOptions.JsonOptionsIndented);
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
            fileWatcher.WatchRepository(targetPath, state.IncludePatterns, state.ExcludePatterns);

            // Register the collection too. Watching without this is the broken half-state: changed
            // files reach the L1 cache, promotion cannot resolve where they belong, and the write is
            // dropped — so the edits vanish at shutdown and the index goes quietly stale. SearchTools
            // .EnsureWatching always did both; this path only ever did the first. state.CollectionName
            // is the authoritative name the indexer actually created, so nothing is derived here.
            l2Promotion.RegisterRepositoryCollection(targetPath, state.CollectionName);
            bool restartStateSaved = activeRepositoryStore.TrySave(state.RepositoryName);

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
                restartStateSaved,
                message = $"Now watching '{repositoryName}' exclusively"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting active repository to {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            string? restartRepository = activeRepositoryStore.TryLoad();

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                watchedRepositories = watched,
                watchedCount = watched.Count,
                hotCacheFileCount = hotCacheCount,
                pendingPromotions = l2Promotion.PendingCount,
                restartRepository,
                // Non-zero means edits were cached but never written to Qdrant — they are lost on
                // shutdown and the index is stale for those files. Should always be 0.
                droppedPromotions = l2Promotion.DroppedPromotionCount
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting watched repositories");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
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

            bool restartStateCleared = activeRepositoryStore.TryClear();

            logger.LogInformation(
                "Stopped watching {Count} repositories. Cache cleared: {CacheCleared}",
                watched.Count, cacheCleared);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                stoppedWatching = watched,
                stoppedCount = watched.Count,
                cacheCleared,
                restartStateCleared,
                message = watched.Count > 0
                    ? $"Stopped watching {watched.Count} repositories"
                    : "No repositories were being watched"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping all watchers");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }
}
