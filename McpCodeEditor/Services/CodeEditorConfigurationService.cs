using Microsoft.Extensions.Configuration;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public class SecuritySettings
{
    public bool RestrictToWorkspace { get; set; } = true;
    public bool AllowExecutableFiles { get; set; } = false;
    public bool AllowHiddenFiles { get; set; } = false;
    public List<string> BlockedPaths { get; set; } = [];
}

public class CodeAnalysisSettings
{
    public bool EnableSyntaxHighlighting { get; set; } = true;
    public bool EnableCodeCompletion { get; set; } = true;
    public bool EnableDiagnostics { get; set; } = true;
    public bool EnableFormatting { get; set; } = true;
    public int MaxAnalysisFileSize { get; set; } = 1024 * 1024; // 1MB
}

public class WorkspaceSettings
{
    public bool AutoDetectWorkspace { get; set; } = true;
    public string PreferredWorkspace { get; set; } = string.Empty;
    public List<string> WorkspaceHistory { get; set; } = [];
}

public class CodeEditorConfiguration
{
    public string DefaultWorkspace { get; set; } = Environment.CurrentDirectory;
    public string LogLevel { get; set; } = "Info";
    public SecuritySettings Security { get; set; } = new();
    public CodeAnalysisSettings CodeAnalysis { get; set; } = new();
    public WorkspaceSettings Workspace { get; set; } = new();
    public List<string> AllowedExtensions { get; set; } = [];
    public List<string> ExcludedDirectories { get; set; } = [];
    public int MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public int MaxFilesPerOperation { get; set; } = 1000;
}

public class CodeEditorConfigurationService
{
    private readonly CodeEditorConfiguration _configuration;
    private readonly ProjectDetectionService _projectDetection;
    private readonly IConfigurationPersistence _persistence;
    private string? _smartWorkspace = null;

    public CodeEditorConfigurationService(
        IConfiguration configuration, 
        ProjectDetectionService projectDetection,
        IConfigurationPersistence persistence)
    {
        _persistence = persistence;
        _projectDetection = projectDetection;
        
        // Load base configuration from appsettings
        _configuration = new CodeEditorConfiguration();
        configuration.GetSection("CodeEditor").Bind(_configuration);
        
        // Load and merge persisted user preferences
        LoadPersistedConfigurationAsync().ConfigureAwait(false);
        
        // Initialize the smart workspace detection
        InitializeWorkspaceAsync().ConfigureAwait(false);
    }

    private async Task LoadPersistedConfigurationAsync()
    {
        try
        {
            var persistedConfig = await _persistence.LoadConfigurationAsync();
            if (persistedConfig?.Workspace != null)
            {
                // Merge workspace settings from persistent storage
                // This ensures user preferences override defaults
                _configuration.Workspace.PreferredWorkspace = persistedConfig.Workspace.PreferredWorkspace;
                _configuration.Workspace.AutoDetectWorkspace = persistedConfig.Workspace.AutoDetectWorkspace;
                _configuration.Workspace.WorkspaceHistory = persistedConfig.Workspace.WorkspaceHistory ?? [];
                
                Console.WriteLine($"Loaded workspace preference: {_configuration.Workspace.PreferredWorkspace}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load persisted configuration: {ex.Message}");
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            await _persistence.SaveConfigurationAsync(_configuration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save configuration: {ex.Message}");
        }
    }

    private async Task InitializeWorkspaceAsync()
    {
        try
        {
            // If auto-detect is enabled and no preferred workspace is set
            if (_configuration.Workspace.AutoDetectWorkspace &&
                string.IsNullOrEmpty(_configuration.Workspace.PreferredWorkspace))
            {
                var suggested = await _projectDetection.SuggestBestWorkspaceAsync();
                if (suggested != null && !IsSystemDirectory(suggested.Path))
                {
                    _smartWorkspace = suggested.Path;

                    // Add to history if not already present
                    if (!_configuration.Workspace.WorkspaceHistory.Contains(suggested.Path))
                    {
                        _configuration.Workspace.WorkspaceHistory.Insert(0, suggested.Path);
                        // Keep only the last 10 workspaces
                        if (_configuration.Workspace.WorkspaceHistory.Count > 10)
                        {
                            _configuration.Workspace.WorkspaceHistory =
                                _configuration.Workspace.WorkspaceHistory.Take(10).ToList();
                        }
                        
                        // Save updated history
                        await SaveConfigurationAsync();
                    }
                }
            }
        }
        catch
        {
            // If detection fails, fall back to default behavior
            _smartWorkspace = null;
        }
    }

    public CodeEditorConfiguration Configuration => _configuration;

    /// <summary>
    /// Gets the effective workspace with FIXED priority logic
    /// </summary>
    public string DefaultWorkspace
    {
        get
        {
            // FIXED Priority order - User preference ALWAYS wins:
            // 1. Preferred workspace (explicitly set by user) - ABSOLUTE PRIORITY
            // 2. Smart-detected workspace (advisory only)
            // 3. Configured default workspace
            // 4. Current directory (fallback)

            string workspace;

            // CRITICAL FIX: User preference has absolute priority
            if (!string.IsNullOrEmpty(_configuration.Workspace.PreferredWorkspace) &&
                Directory.Exists(_configuration.Workspace.PreferredWorkspace))
            {
                workspace = _configuration.Workspace.PreferredWorkspace;
            }
            else if (!string.IsNullOrEmpty(_smartWorkspace) && Directory.Exists(_smartWorkspace))
            {
                workspace = _smartWorkspace;
            }
            else if (!string.IsNullOrEmpty(_configuration.DefaultWorkspace) &&
                     Directory.Exists(_configuration.DefaultWorkspace))
            {
                workspace = _configuration.DefaultWorkspace;
            }
            else
            {
                workspace = Environment.CurrentDirectory;
            }

            // Ensure we return an absolute path
            return Path.IsPathRooted(workspace) ? workspace : Path.GetFullPath(workspace);
        }
    }

    /// <summary>
    /// Gets the smart-detected workspace, if any
    /// </summary>
    public string? SmartDetectedWorkspace => _smartWorkspace;

    /// <summary>
    /// Gets the source of the current workspace (user preference, smart detection, or default)
    /// </summary>
    public string GetWorkspaceSource()
    {
        if (!string.IsNullOrEmpty(_configuration.Workspace.PreferredWorkspace) &&
            Directory.Exists(_configuration.Workspace.PreferredWorkspace))
        {
            return "user_preference";
        }
        if (!string.IsNullOrEmpty(_smartWorkspace) && Directory.Exists(_smartWorkspace))
        {
            return "smart_detection";
        }
        if (!string.IsNullOrEmpty(_configuration.DefaultWorkspace) &&
            Directory.Exists(_configuration.DefaultWorkspace))
        {
            return "default_config";
        }
        return "current_directory";
    }

    /// <summary>
    /// Sets the preferred workspace (overrides auto-detection) - NOW WITH PERSISTENCE
    /// </summary>
    public async Task SetPreferredWorkspaceAsync(string workspace)
    {
        if (Directory.Exists(workspace))
        {
            _configuration.Workspace.PreferredWorkspace = Path.GetFullPath(workspace);

            // Add to history
            if (!_configuration.Workspace.WorkspaceHistory.Contains(workspace))
            {
                _configuration.Workspace.WorkspaceHistory.Insert(0, workspace);
                if (_configuration.Workspace.WorkspaceHistory.Count > 10)
                {
                    _configuration.Workspace.WorkspaceHistory =
                        _configuration.Workspace.WorkspaceHistory.Take(10).ToList();
                }
            }

            // CRITICAL FIX: Save to persistent storage
            await SaveConfigurationAsync();
            
            Console.WriteLine($"Workspace preference saved: {workspace}");
        }
    }

    /// <summary>
    /// Clear workspace preference (allows smart detection to take over)
    /// </summary>
    public async Task ClearWorkspacePreferenceAsync()
    {
        _configuration.Workspace.PreferredWorkspace = string.Empty;
        await SaveConfigurationAsync();
        Console.WriteLine("Workspace preference cleared - smart detection will be used");
    }

    /// <summary>
    /// Re-runs workspace detection
    /// </summary>
    public async Task RefreshWorkspaceDetectionAsync()
    {
        await InitializeWorkspaceAsync();
    }

    public SecuritySettings Security => _configuration.Security;
    public CodeAnalysisSettings CodeAnalysis => _configuration.CodeAnalysis;
    public WorkspaceSettings Workspace => _configuration.Workspace;
    public List<string> AllowedExtensions => _configuration.AllowedExtensions;
    public List<string> ExcludedDirectories => _configuration.ExcludedDirectories;
    public int MaxFileSize => _configuration.MaxFileSize;
    public int MaxFilesPerOperation => _configuration.MaxFilesPerOperation;

    private static bool IsSystemDirectory(string path)
    {
        var systemPaths = new[]
        {
            "C:\\Windows", "C:\\Program Files", "C:\\Program Files (x86)",
            "/usr", "/bin", "/sbin", "/etc", "/var", "/sys", "/proc"
        };

        return systemPaths.Any(sysPath =>
            path.StartsWith(sysPath, StringComparison.OrdinalIgnoreCase));
    }
}
