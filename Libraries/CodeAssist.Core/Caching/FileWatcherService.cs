using System.Collections.Concurrent;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Caching;

/// <summary>
/// Watches for file changes in indexed repositories and updates the L1 hot cache.
/// Uses debouncing to avoid excessive updates during rapid edits.
/// </summary>
public sealed class FileWatcherService(
    HotCache hotCache,
    ChunkerFactory chunkerFactory,
    IOptions<CodeAssistOptions> options,
    ILogger<FileWatcherService> logger)
    : IDisposable
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();
    private readonly ConcurrentDictionary<string, string> _repositoryRoots = new(); // filePath -> repoRoot
    private readonly CodeAssistOptions _options = options.Value;
    private bool _disposed;

    /// <summary>
    /// Start watching a repository for file changes.
    /// </summary>
    public void WatchRepository(string repositoryRoot, IEnumerable<string>? includePatterns = null)
    {
        if (!_options.EnableFileWatcher)
        {
            logger.LogDebug("File watcher disabled, skipping watch for {Root}", repositoryRoot);
            return;
        }

        string normalizedRoot = Path.GetFullPath(repositoryRoot);

        if (_watchers.ContainsKey(normalizedRoot))
        {
            logger.LogDebug("Already watching repository: {Root}", normalizedRoot);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(normalizedRoot)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnWatcherError;

            _watchers[normalizedRoot] = watcher;

            logger.LogInformation("Started watching repository: {Root}", normalizedRoot);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start watcher for repository: {Root}", normalizedRoot);
        }
    }

    /// <summary>
    /// Stop watching a repository.
    /// </summary>
    public void StopWatching(string repositoryRoot)
    {
        string normalizedRoot = Path.GetFullPath(repositoryRoot);

        if (_watchers.TryRemove(normalizedRoot, out FileSystemWatcher? watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            logger.LogInformation("Stopped watching repository: {Root}", normalizedRoot);
        }

        // Clear hot cache entries for this repository
        hotCache.ClearRepository(repositoryRoot);

        // Remove repository root mappings
        List<string> keysToRemove = _repositoryRoots
            .Where(kvp => kvp.Value == normalizedRoot)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string key in keysToRemove)
        {
            _repositoryRoots.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Check if a repository is being watched.
    /// </summary>
    public bool IsWatching(string repositoryRoot)
    {
        string normalizedRoot = Path.GetFullPath(repositoryRoot);
        return _watchers.ContainsKey(normalizedRoot);
    }

    /// <summary>
    /// Get all watched repositories.
    /// </summary>
    public IReadOnlyList<string> GetWatchedRepositories()
    {
        return _watchers.Keys.ToList();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath)) return;

        logger.LogDebug("File changed: {Path}", e.FullPath);
        DebouncedUpdate(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath)) return;

        logger.LogDebug("File deleted: {Path}", e.FullPath);
        hotCache.Remove(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Remove old path
        if (!ShouldIgnoreFile(e.OldFullPath))
        {
            hotCache.Remove(e.OldFullPath);
        }

        // Add new path
        if (ShouldIgnoreFile(e.FullPath)) return;
        logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        DebouncedUpdate(e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        logger.LogError(e.GetException(), "File watcher error");
    }

    private void DebouncedUpdate(string filePath)
    {
        string normalizedPath = Path.GetFullPath(filePath);

        // Cancel any pending debounce for this file
        if (_debounceTokens.TryRemove(normalizedPath, out CancellationTokenSource? existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _debounceTokens[normalizedPath] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_options.FileWatcherDebounceDelay, cts.Token);

                // Debounce completed, update cache
                _debounceTokens.TryRemove(normalizedPath, out _);

                string? repositoryRoot = GetRepositoryRoot(normalizedPath);
                if (repositoryRoot == null)
                {
                    logger.LogDebug("No repository root found for {Path}, skipping cache update", normalizedPath);
                    return;
                }

                await hotCache.UpdateFileAsync(normalizedPath, repositoryRoot, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Debounce was cancelled by a newer change, ignore
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating cache for {Path}", normalizedPath);
            }
            finally
            {
                cts.Dispose();
            }
        }, cts.Token);
    }

    private string? GetRepositoryRoot(string filePath)
    {
        // Check if we have a registered mapping
        if (_repositoryRoots.TryGetValue(filePath, out string? registeredRoot))
        {
            return registeredRoot;
        }

        // Find which watcher this file belongs to
        foreach ((string root, FileSystemWatcher _) in _watchers)
        {
            if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            _repositoryRoots[filePath] = root; // Cache for future lookups
            return root;
        }

        return null;
    }

    private bool ShouldIgnoreFile(string filePath)
    {
        // Ignore directories
        if (Directory.Exists(filePath))
            return true;

        // Check if file type is supported
        string extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
            return true;

        if (!ChunkerFactory.IsSupportedExtension(extension))
            return true;

        // Check against exclude patterns
        string relativePath = filePath;
        foreach ((string root, FileSystemWatcher _) in _watchers)
        {
            if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            relativePath = Path.GetRelativePath(root, filePath);
            break;
        }

        return _options.DefaultExcludePatterns.Any(pattern => MatchesPattern(relativePath, pattern));
    }

    private static bool MatchesPattern(string path, string pattern)
    {
        // Simple glob matching for common patterns
        string normalizedPath = path.Replace('\\', '/');
        string normalizedPattern = pattern.Replace('\\', '/');

        if (!normalizedPattern.StartsWith("**/")) return normalizedPath.EndsWith(normalizedPattern.TrimStart('*'));
        string suffix = normalizedPattern[3..];
        if (!suffix.EndsWith("/**")) return normalizedPath.EndsWith(suffix.TrimStart('*'));
        // Pattern like "**/bin/**" - check if path contains the directory
        string dir = suffix[..^3];
        return normalizedPath.Contains($"/{dir}/") ||
               normalizedPath.StartsWith($"{dir}/") ||
               normalizedPath.EndsWith($"/{dir}");
        // Pattern like "**/*.min.js" - check suffix

    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach ((string _, FileSystemWatcher watcher) in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        foreach ((string _, CancellationTokenSource cts) in _debounceTokens)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _debounceTokens.Clear();

        logger.LogInformation("FileWatcherService disposed");
    }
}
