using System.Text.Json;
using DesktopCommander.Common;

namespace DesktopCommander.Services;

public enum FileAccessType
{
    Read,
    Write,
    Delete
}

public class SecurityManager
{
    private readonly List<string> _allowedDirectories = [];
    private readonly HashSet<string> _blockedCommands = [];
    private readonly ILogger<SecurityManager> _logger;
    private readonly string _configPath;

    public SecurityManager(ILogger<SecurityManager> logger)
    {
        _logger = logger;
        _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopDriver", "config.json");
        LoadConfiguration();
    }

    public bool IsDirectoryAllowed(string path)
    {
        if (_allowedDirectories.Count == 0)
            return true; // If no restrictions, allow all

        string normalizedPath = Path.GetFullPath(path);
        return _allowedDirectories.Any(allowed => 
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }
    
    public void ValidateFileAccess(string filePath, FileAccessType accessType)
    {
        string directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("Invalid file path", nameof(filePath));
    
        if (!IsDirectoryAllowed(directory))
        {
            throw new UnauthorizedAccessException($"Access denied to file: {filePath}. Directory not in allowed list.");
        }
    
        // Optional: Additional validation based on access type
        switch (accessType)
        {
            case FileAccessType.Read:
                // Could check if the file exists and is readable
                break;
            case FileAccessType.Write:
                // Could check if the directory is writable
                break;
            case FileAccessType.Delete:
                // Could add extra protection for critical files
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null);
        }
    }

    public bool IsCommandBlocked(string command)
    {
        string cmdLower = command.ToLowerInvariant();
        return _blockedCommands.Any(blocked => cmdLower.Contains(blocked));
    }

    public void AddAllowedDirectory(string directory)
    {
        if (_allowedDirectories.Contains(directory)) return;
        _allowedDirectories.Add(directory);
        SaveConfiguration();
    }

    public void AddBlockedCommand(string command)
    {
        _blockedCommands.Add(command.ToLowerInvariant());
        SaveConfiguration();
    }

    public IReadOnlyList<string> AllowedDirectories => _allowedDirectories.AsReadOnly();
    public IReadOnlySet<string> BlockedCommands => _blockedCommands.ToHashSet();

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<SecurityConfig>(json);
                if (config is null) return;
                _allowedDirectories.AddRange(config.AllowedDirectories ?? []);
                foreach (string cmd in config.BlockedCommands ?? [])
                {
                    _blockedCommands.Add(cmd.ToLowerInvariant());
                }
            }
            else
            {
                // Set default security settings
                SetDefaultSecuritySettings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load security configuration");
            SetDefaultSecuritySettings();
        }
    }

    private void SetDefaultSecuritySettings()
    {
        // Add the current user directory as default allowed
        _allowedDirectories.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        
        // Add default blocked commands for security
        var defaultBlocked = new[]
        {
            "rm -rf", "del /f", "format", "fdisk", "mkfs", "mount", "umount",
            "shutdown", "reboot", "halt", "poweroff", "sudo", "su", "passwd",
            "adduser", "useradd", "usermod", "groupadd", "visudo", "iptables",
            "firewall", "netsh", "sfc", "bcdedit", "reg", "takeown", "cipher"
        };
        
        foreach (string cmd in defaultBlocked)
        {
            _blockedCommands.Add(cmd.ToLowerInvariant());
        }
        
        SaveConfiguration();
    }

    private void SaveConfiguration()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            var config = new SecurityConfig
            {
                AllowedDirectories = _allowedDirectories.ToArray(),
                BlockedCommands = _blockedCommands.ToArray()
            };
            string json = JsonSerializer.Serialize(config, SerializerOptions.JsonOptionsIndented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save security configuration");
        }
    }

    private class SecurityConfig
    {
        public string[]? AllowedDirectories { get; init; }
        public string[]? BlockedCommands { get; init; }
    }
}
