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
public sealed class FileWatcherService : IDisposable
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();
    private readonly ConcurrentDictionary<string, string> _repositoryRoots = new(); // filePath -> repoRoot
    private readonly HotCache _hotCache;
    private readonly ChunkerFactory _chunkerFactory;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<FileWatcherService> _logger;
    private bool _disposed;

    public FileWatcherService(
        HotCache hotCache,
        ChunkerFactory chunkerFactory,
        IOptions<CodeAssistOptions> options,
        ILogger<FileWatcherService> logger)
    {
        _hotCache = hotCache;
        _chunkerFactory = chunkerFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Start watching a repository for file changes.
    /// </summary>
    public void WatchRepository(string repositoryRoot, IEnumerable<string>? includePatterns = null)
    {
        if (!_options.EnableFileWatcher)
        {
            _logger.LogDebug("File watcher disabled, skipping watch for {Root}", repositoryRoot);
            return;
        }

        var normalizedRoot = Path.GetFullPath(repositoryRoot);

        if (_watchers.ContainsKey(normalizedRoot))
        {
            _logger.LogDebug("Already watching repository: {Root}", normalizedRoot);
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

            _logger.LogInformation("Started watching repository: {Root}", normalizedRoot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watcher for repository: {Root}", normalizedRoot);
        }
    }

    /// <summary>
    /// Stop watching a repository.
    /// </summary>
    public void StopWatching(string repositoryRoot)
    {
        var normalizedRoot = Path.GetFullPath(repositoryRoot);

        if (_watchers.TryRemove(normalizedRoot, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _logger.LogInformation("Stopped watching repository: {Root}", normalizedRoot);
        }

        // Clear hot cache entries for this repository
        _hotCache.ClearRepository(repositoryRoot);

        // Remove repository root mappings
        var keysToRemove = _repositoryRoots
            .Where(kvp => kvp.Value == normalizedRoot)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _repositoryRoots.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Check if a repository is being watched.
    /// </summary>
    public bool IsWatching(string repositoryRoot)
    {
        var normalizedRoot = Path.GetFullPath(repositoryRoot);
        return _watchers.ContainsKey(normalizedRoot);
    }

    /// <summary>
    /// Get all watched repositories.
    /// </summary>
    public IReadOnlyList<string> GetWatchedRepositories()
    {
        return _watchers.Keys.ToList();
    }

    /// <summary>
    /// Register a file path with its repository root for proper cache updates.
    /// </summary>
    public void RegisterFile(string filePath, string repositoryRoot)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var normalizedRoot = Path.GetFullPath(repositoryRoot);
        _repositoryRoots[normalizedPath] = normalizedRoot;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath)) return;

        _logger.LogDebug("File changed: {Path}", e.FullPath);
        DebouncedUpdate(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath)) return;

        _logger.LogDebug("File deleted: {Path}", e.FullPath);
        _hotCache.Remove(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Remove old path
        if (!ShouldIgnoreFile(e.OldFullPath))
        {
            _hotCache.Remove(e.OldFullPath);
        }

        // Add new path
        if (!ShouldIgnoreFile(e.FullPath))
        {
            _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            DebouncedUpdate(e.FullPath);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File watcher error");
    }

    private void DebouncedUpdate(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);

        // Cancel any pending debounce for this file
        if (_debounceTokens.TryRemove(normalizedPath, out var existingCts))
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

                var repositoryRoot = GetRepositoryRoot(normalizedPath);
                if (repositoryRoot == null)
                {
                    _logger.LogDebug("No repository root found for {Path}, skipping cache update", normalizedPath);
                    return;
                }

                await _hotCache.UpdateFileAsync(normalizedPath, repositoryRoot, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Debounce was cancelled by a newer change, ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cache for {Path}", normalizedPath);
            }
            finally
            {
                cts.Dispose();
            }
        });
    }

    private string? GetRepositoryRoot(string filePath)
    {
        // Check if we have a registered mapping
        if (_repositoryRoots.TryGetValue(filePath, out var registeredRoot))
        {
            return registeredRoot;
        }

        // Find which watcher this file belongs to
        foreach (var (root, _) in _watchers)
        {
            if (filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                _repositoryRoots[filePath] = root; // Cache for future lookups
                return root;
            }
        }

        return null;
    }

    private bool ShouldIgnoreFile(string filePath)
    {
        // Ignore directories
        if (Directory.Exists(filePath))
            return true;

        // Check if file type is supported
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
            return true;

        if (!_chunkerFactory.IsSupportedExtension(extension))
            return true;

        // Check against exclude patterns
        var relativePath = filePath;
        foreach (var (root, _) in _watchers)
        {
            if (filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = Path.GetRelativePath(root, filePath);
                break;
            }
        }

        foreach (var pattern in _options.DefaultExcludePatterns)
        {
            if (MatchesPattern(relativePath, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string path, string pattern)
    {
        // Simple glob matching for common patterns
        var normalizedPath = path.Replace('\\', '/');
        var normalizedPattern = pattern.Replace('\\', '/');

        if (normalizedPattern.StartsWith("**/"))
        {
            var suffix = normalizedPattern[3..];
            if (suffix.EndsWith("/**"))
            {
                // Pattern like "**/bin/**" - check if path contains the directory
                var dir = suffix[..^3];
                return normalizedPath.Contains($"/{dir}/") ||
                       normalizedPath.StartsWith($"{dir}/") ||
                       normalizedPath.EndsWith($"/{dir}");
            }
            // Pattern like "**/*.min.js" - check suffix
            return normalizedPath.EndsWith(suffix.TrimStart('*'));
        }

        return normalizedPath.EndsWith(normalizedPattern.TrimStart('*'));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (_, watcher) in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        foreach (var (_, cts) in _debounceTokens)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _debounceTokens.Clear();

        _logger.LogInformation("FileWatcherService disposed");
    }
}
