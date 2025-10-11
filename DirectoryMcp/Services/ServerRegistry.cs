using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DirectoryMcp.Services;

/// <summary>
/// Registry that loads MCP servers from servers.json
/// </summary>
public class ServerRegistry
{
    private readonly ILogger<ServerRegistry> _logger;
    private Dictionary<string, ServerInfo> _servers = new();
    private readonly string _configPath;

    public ServerRegistry(ILogger<ServerRegistry> logger)
    {
        _logger = logger;
        
        // Look for servers.json in the same directory as the executable
        _configPath = Path.Combine(AppContext.BaseDirectory, "servers.json");
        
        LoadServers();
    }

    private void LoadServers()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("servers.json not found at {Path}. Creating default configuration.", _configPath);
                CreateDefaultConfiguration();
                return;
            }

            string json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<ServerConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config?.Servers != null)
            {
                _servers = config.Servers;
                _logger.LogInformation("Loaded {Count} servers from configuration", _servers.Count);
            }
            else
            {
                _logger.LogWarning("No servers found in configuration");
                _servers = new Dictionary<string, ServerInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading servers.json from {Path}", _configPath);
            _servers = new Dictionary<string, ServerInfo>();
        }
    }

    private void CreateDefaultConfiguration()
    {
        var defaultConfig = new ServerConfiguration
        {
            Servers = new Dictionary<string, ServerInfo>
            {
                ["redis"] = new ServerInfo
                {
                    Name = "Redis Tools",
                    Url = "https://localhost:7183"
                },
                ["pdf"] = new ServerInfo
                {
                    Name = "PDF Tools",
                    Url = "https://localhost:7002"
                },
                ["office"] = new ServerInfo
                {
                    Name = "Office Tools",
                    Url = "https://localhost:7030"
                },
                ["desktop-commander"] = new ServerInfo
                {
                    Name = "Desktop Tools",
                    Url = "https://localhost:7117"
                }
            }
        };

        try
        {
            string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_configPath, json);
            _servers = defaultConfig.Servers;
            _logger.LogInformation("Created default servers.json at {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default servers.json");
            _servers = defaultConfig.Servers;
        }
    }

    /// <summary>
    /// Get all registered servers
    /// </summary>
    public Dictionary<string, object> GetAllServers()
    {
        return _servers.ToDictionary(
            kvp => kvp.Key, object (kvp) => new
            {
                name = kvp.Value.Name,
                url = kvp.Value.Url
            }
        );
    }

    /// <summary>
    /// Get detailed information about all servers
    /// </summary>
    public Dictionary<string, ServerInfo> GetAllServerDetails()
    {
        return new Dictionary<string, ServerInfo>(_servers);
    }

    /// <summary>
    /// Get server information by key
    /// </summary>
    public ServerInfo? GetServer(string key)
    {
        return _servers.GetValueOrDefault(key);
    }

    /// <summary>
    /// Reload servers from the configuration file
    /// </summary>
    public void Reload()
    {
        LoadServers();
    }
}

public class ServerConfiguration
{
    public Dictionary<string, ServerInfo> Servers { get; set; } = new();
}

public class ServerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}