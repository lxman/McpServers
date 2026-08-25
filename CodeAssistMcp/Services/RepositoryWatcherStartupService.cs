using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeAssistMcp.Services;

/// <summary>
/// Restores the active repository's watcher after a process restart.
/// </summary>
public sealed class RepositoryWatcherStartupService(
    ActiveRepositoryStore activeRepositoryStore,
    RepositoryIndexer indexer,
    FileWatcherService fileWatcher,
    L2PromotionService l2Promotion,
    ILogger<RepositoryWatcherStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? repositoryName = activeRepositoryStore.TryLoad();
        if (repositoryName is null) return;

        try
        {
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
            if (state is null)
            {
                logger.LogWarning(
                    "Cannot restore watcher for {Repository}: its index state no longer exists",
                    repositoryName);
                activeRepositoryStore.TryClear();
                return;
            }

            if (!Directory.Exists(state.RootPath))
            {
                logger.LogWarning(
                    "Cannot restore watcher for {Repository}: repository path {Path} does not exist",
                    repositoryName, state.RootPath);
                return;
            }

            fileWatcher.WatchRepository(state.RootPath, state.IncludePatterns, state.ExcludePatterns);
            l2Promotion.RegisterRepositoryCollection(state.RootPath, state.CollectionName);
            logger.LogInformation("Restored watcher for active repository {Repository} at {Path}",
                repositoryName, state.RootPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A bad persisted watcher must remain diagnosable without preventing the MCP server from
            // starting and serving explicit repair/status tools.
            logger.LogError(ex, "Failed to restore watcher for active repository {Repository}", repositoryName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
