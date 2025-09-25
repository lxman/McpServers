using System.Text.Json;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for managing workspace metadata including hash mappings and access tracking
/// Stores metadata in %APPDATA%\McpCodeEditor\metadata\workspace-mappings.json
/// </summary>
public class WorkspaceMetadataService : IWorkspaceMetadataService
{
    private readonly IAppDataPathService _appDataPathService;
    private readonly ILogger<WorkspaceMetadataService> _logger;
    private readonly string _metadataFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Cache for frequently accessed metadata
    private readonly Dictionary<string, WorkspaceMetadata> _metadataCache = new();
    private DateTime _lastCacheRefresh = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

    public WorkspaceMetadataService(IAppDataPathService appDataPathService, ILogger<WorkspaceMetadataService> logger)
    {
        _appDataPathService = appDataPathService ?? throw new ArgumentNullException(nameof(appDataPathService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Set up metadata file path: %APPDATA%\McpCodeEditor\metadata\workspace-mappings.json
        var metadataDir = Path.Combine(_appDataPathService.GetAppDataRoot(), "metadata");
        _appDataPathService.EnsureDirectoryExists(metadataDir);
        _metadataFilePath = Path.Combine(metadataDir, "workspace-mappings.json");

        _logger.LogDebug("WorkspaceMetadataService initialized with metadata file: {MetadataFilePath}", _metadataFilePath);
    }

    public async Task<WorkspaceMetadata?> GetWorkspaceMetadataAsync(string workspaceHash)
    {
        if (string.IsNullOrEmpty(workspaceHash))
        {
            throw new ArgumentException("Workspace hash cannot be null or empty", nameof(workspaceHash));
        }

        var allMetadata = await LoadAllMetadataAsync();
        return allMetadata.FirstOrDefault(m => m.Hash == workspaceHash);
    }

    public async Task<WorkspaceMetadata?> GetWorkspaceMetadataByPathAsync(string workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            throw new ArgumentException("Workspace path cannot be null or empty", nameof(workspacePath));
        }

        var normalizedPath = _appDataPathService.NormalizeWorkspacePath(workspacePath);
        var allMetadata = await LoadAllMetadataAsync();
        
        // First try to find by current path
        var metadata = allMetadata.FirstOrDefault(m => 
            _appDataPathService.NormalizeWorkspacePath(m.CurrentPath) == normalizedPath);
        
        if (metadata == null)
        {
            // Try to find by original path
            metadata = allMetadata.FirstOrDefault(m => 
                _appDataPathService.NormalizeWorkspacePath(m.OriginalPath) == normalizedPath);
        }

        return metadata;
    }

    public async Task<WorkspaceMetadata> CreateOrUpdateWorkspaceMetadataAsync(string workspacePath, string? displayName = null)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            throw new ArgumentException("Workspace path cannot be null or empty", nameof(workspacePath));
        }

        var workspaceHash = _appDataPathService.GetWorkspaceHash(workspacePath);
        var existing = await GetWorkspaceMetadataAsync(workspaceHash);

        if (existing != null)
        {
            // Update existing metadata
            var oldPath = existing.CurrentPath;
            existing.CurrentPath = workspacePath;
            existing.LastAccessed = DateTime.UtcNow;
            existing.AccessCount++;

            // Update display name if provided
            if (!string.IsNullOrEmpty(displayName))
            {
                existing.DisplayName = displayName;
            }

            // Log if path changed
            if (oldPath != workspacePath)
            {
                _logger.LogInformation("Workspace path updated for {Hash}: {OldPath} -> {NewPath}", 
                    workspaceHash, oldPath, workspacePath);
            }

            await SaveMetadataAsync(existing);
            return existing;
        }
        else
        {
            // Create new metadata
            var metadata = new WorkspaceMetadata
            {
                Hash = workspaceHash,
                CurrentPath = workspacePath,
                OriginalPath = workspacePath,
                DisplayName = displayName ?? GetDisplayNameFromPath(workspacePath),
                LastAccessed = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                AccessCount = 1
            };

            await SaveMetadataAsync(metadata);
            _logger.LogInformation("Created new workspace metadata for {Hash}: {DisplayName} at {Path}", 
                workspaceHash, metadata.DisplayName, workspacePath);

            return metadata;
        }
    }

    public async Task<WorkspaceMetadata> UpdateLastAccessedAsync(string workspacePath)
    {
        return await CreateOrUpdateWorkspaceMetadataAsync(workspacePath);
    }

    public async Task<WorkspaceMetadata?> UpdateWorkspacePathAsync(string workspaceHash, string newPath)
    {
        if (string.IsNullOrEmpty(workspaceHash))
        {
            throw new ArgumentException("Workspace hash cannot be null or empty", nameof(workspaceHash));
        }

        if (string.IsNullOrEmpty(newPath))
        {
            throw new ArgumentException("New path cannot be null or empty", nameof(newPath));
        }

        var metadata = await GetWorkspaceMetadataAsync(workspaceHash);
        if (metadata == null)
        {
            return null;
        }

        var oldPath = metadata.CurrentPath;
        metadata.CurrentPath = newPath;
        metadata.LastAccessed = DateTime.UtcNow;

        await SaveMetadataAsync(metadata);
        _logger.LogInformation("Updated workspace path for {Hash}: {OldPath} -> {NewPath}", 
            workspaceHash, oldPath, newPath);

        return metadata;
    }

    public async Task<List<WorkspaceMetadata>> GetAllWorkspaceMetadataAsync()
    {
        return await LoadAllMetadataAsync();
    }

    public async Task<List<WorkspaceMetadata>> GetRecentWorkspacesAsync(int maxCount = 10)
    {
        var allMetadata = await LoadAllMetadataAsync();
        return allMetadata
            .OrderByDescending(m => m.LastAccessed)
            .Take(maxCount)
            .ToList();
    }

    public async Task<bool> RemoveWorkspaceMetadataAsync(string workspaceHash)
    {
        if (string.IsNullOrEmpty(workspaceHash))
        {
            throw new ArgumentException("Workspace hash cannot be null or empty", nameof(workspaceHash));
        }

        var allMetadata = await LoadAllMetadataAsync();
        var toRemove = allMetadata.FirstOrDefault(m => m.Hash == workspaceHash);
        
        if (toRemove == null)
        {
            return false;
        }

        allMetadata.Remove(toRemove);
        await SaveAllMetadataAsync(allMetadata);
        
        // Remove from cache
        _metadataCache.Remove(workspaceHash);
        
        _logger.LogInformation("Removed workspace metadata for {Hash}: {DisplayName}", 
            workspaceHash, toRemove.DisplayName);
        
        return true;
    }

    public async Task<List<WorkspaceMetadata>> GetUnusedWorkspacesAsync(int daysUnused = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysUnused);
        var allMetadata = await LoadAllMetadataAsync();
        
        return allMetadata
            .Where(m => m.LastAccessed < cutoffDate)
            .OrderBy(m => m.LastAccessed)
            .ToList();
    }

    public async Task<WorkspaceMetadata?> UpdateWorkspaceTagsAsync(string workspaceHash, List<string> tags)
    {
        var metadata = await GetWorkspaceMetadataAsync(workspaceHash);
        if (metadata == null)
        {
            return null;
        }

        metadata.Tags = tags ?? [];
        await SaveMetadataAsync(metadata);
        
        return metadata;
    }

    public async Task<WorkspaceMetadata?> UpdateWorkspaceNotesAsync(string workspaceHash, string notes)
    {
        var metadata = await GetWorkspaceMetadataAsync(workspaceHash);
        if (metadata == null)
        {
            return null;
        }

        metadata.Notes = notes ?? string.Empty;
        await SaveMetadataAsync(metadata);
        
        return metadata;
    }

    public async Task<List<WorkspaceMetadata>> FindWorkspacesByNameAsync(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return [];
        }

        var allMetadata = await LoadAllMetadataAsync();
        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        
        return allMetadata
            .Where(m => m.DisplayName.Contains(lowerSearchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                       m.CurrentPath.ToLowerInvariant().Contains(lowerSearchTerm))
            .OrderByDescending(m => m.LastAccessed)
            .ToList();
    }

    private async Task<List<WorkspaceMetadata>> LoadAllMetadataAsync()
    {
        // Check cache first
        if (DateTime.UtcNow - _lastCacheRefresh < _cacheTimeout && _metadataCache.Count != 0)
        {
            return _metadataCache.Values.ToList();
        }

        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_metadataFilePath))
            {
                _logger.LogDebug("Metadata file does not exist, returning empty list");
                return [];
            }

            var jsonContent = await File.ReadAllTextAsync(_metadataFilePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogDebug("Metadata file is empty, returning empty list");
                return [];
            }

            var metadata = JsonSerializer.Deserialize<List<WorkspaceMetadata>>(jsonContent) ?? [];
            
            // Update cache
            _metadataCache.Clear();
            foreach (var item in metadata)
            {
                _metadataCache[item.Hash] = item;
            }
            _lastCacheRefresh = DateTime.UtcNow;

            _logger.LogDebug("Loaded {Count} workspace metadata entries from file", metadata.Count);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace metadata from {FilePath}", _metadataFilePath);
            return [];
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveMetadataAsync(WorkspaceMetadata metadata)
    {
        var allMetadata = await LoadAllMetadataAsync();
        
        // Update or add the metadata
        var existing = allMetadata.FirstOrDefault(m => m.Hash == metadata.Hash);
        if (existing != null)
        {
            var index = allMetadata.IndexOf(existing);
            allMetadata[index] = metadata;
        }
        else
        {
            allMetadata.Add(metadata);
        }

        await SaveAllMetadataAsync(allMetadata);
        
        // Update cache
        _metadataCache[metadata.Hash] = metadata;
    }

    private async Task SaveAllMetadataAsync(List<WorkspaceMetadata> allMetadata)
    {
        await _fileLock.WaitAsync();
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var jsonContent = JsonSerializer.Serialize(allMetadata, options);
            await File.WriteAllTextAsync(_metadataFilePath, jsonContent);
            
            _logger.LogDebug("Saved {Count} workspace metadata entries to file", allMetadata.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workspace metadata to {FilePath}", _metadataFilePath);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static string GetDisplayNameFromPath(string workspacePath)
    {
        try
        {
            return Path.GetFileName(workspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) 
                   ?? "Unknown Workspace";
        }
        catch
        {
            return "Unknown Workspace";
        }
    }
}
