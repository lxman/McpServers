using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DesktopDriver.Services;

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
        if (!_allowedDirectories.Any())
            return true; // If no restrictions, allow all

        string normalizedPath = Path.GetFullPath(path);
        return _allowedDirectories.Any(allowed => 
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }

    public bool IsCommandBlocked(string command)
    {
        string cmdLower = command.ToLowerInvariant();
        return _blockedCommands.Any(blocked => cmdLower.Contains(blocked));
    }

    public void AddAllowedDirectory(string directory)
    {
        if (!_allowedDirectories.Contains(directory))
        {
            _allowedDirectories.Add(directory);
            SaveConfiguration();
        }
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
                if (config != null)
                {
                    _allowedDirectories.AddRange(config.AllowedDirectories ?? []);
                    foreach (string cmd in config.BlockedCommands ?? [])
                    {
                        _blockedCommands.Add(cmd.ToLowerInvariant());
                    }
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
        // Add current user directory as default allowed
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
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save security configuration");
        }
    }

    private class SecurityConfig
    {
        public string[]? AllowedDirectories { get; set; }
        public string[]? BlockedCommands { get; set; }
    }
}
