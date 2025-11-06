using System.Text.Json;
using DesktopCommander.Core.Common;
using DesktopCommander.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DesktopCommander.Core.Services;

/// <summary>
/// Registry that loads and caches MCP server configuration from servers.json
/// </summary>
public class ServerRegistry
{
    private readonly ILogger<ServerRegistry> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _configPath;
    private const string CACHE_KEY = "ServerRegistry_Config";

    public ServerRegistry(ILogger<ServerRegistry> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _configPath = Path.Combine(AppContext.BaseDirectory, "servers.json");
        LoadServers();
    }

    private ServerConfiguration LoadServers()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("servers.json not found at {Path}", _configPath);
                return new ServerConfiguration();
            }

            string json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<ServerConfiguration>(json, 
                SerializerOptions.JsonOptionsCaseInsensitiveTrue);

            if (config != null)
            {
                _cache.Set(CACHE_KEY, config, TimeSpan.FromHours(24));
                _logger.LogInformation("Loaded {Count} servers from configuration", config.Servers.Count);
                return config;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading servers.json from {Path}", _configPath);
        }

        return new ServerConfiguration();
    }

    /// <summary>
    /// Get the full server configuration
    /// </summary>
    public ServerConfiguration GetConfiguration()
    {
        if (_cache.TryGetValue(CACHE_KEY, out ServerConfiguration? config) && config != null)
        {
            return config;
        }

        return LoadServers();
    }

    /// <summary>
    /// Get information about a specific server by key
    /// </summary>
    public ServerInfo? GetServer(string key)
    {
        ServerConfiguration config = GetConfiguration();
        return config.Servers.GetValueOrDefault(key);
    }

    /// <summary>
    /// Reload the server configuration from disk
    /// </summary>
    public void Reload()
    {
        _cache.Remove(CACHE_KEY);
        LoadServers();
    }
}