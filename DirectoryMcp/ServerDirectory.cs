using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DirectoryMcp;

/// <summary>
/// Provides a directory of available MCP servers and their API endpoints.
/// </summary>
[McpServerToolType]
public class ServerDirectory(ILogger<ServerDirectory> logger)
{
    private const string CONFIG_FILE_NAME = "servers.json";

    /// <summary>
    /// Lists all available MCP servers with their HTTP API endpoints.
    /// </summary>
    [McpServerTool, DisplayName("list_servers")]
    [Description("Get the directory of available MCP server APIs")]
    public string ListServers()
    {
        logger.LogInformation("list_servers called");

        try
        {
            // Look for the config file in the same directory as the executable
            string configPath = Path.Combine(AppContext.BaseDirectory, CONFIG_FILE_NAME);
            
            if (!File.Exists(configPath))
            {
                logger.LogWarning("Config file not found at: {ConfigPath}", configPath);
                return JsonSerializer.Serialize(new
                {
                    error = "Configuration file not found",
                    expectedPath = configPath,
                    message = "Create a servers.json file with your server configuration"
                }, SerializerOptions.JsonOptionsIndented);
            }

            string jsonContent = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ServerConfig>(jsonContent, SerializerOptions.JsonOptionsCaseInsensitive);

            if (config == null)
            {
                logger.LogError("Failed to deserialize config file");
                return JsonSerializer.Serialize(new
                {
                    error = "Invalid configuration file format"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Loaded {Count} servers from config", config.Servers.Count);
            return JsonSerializer.Serialize(config, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading server configuration");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                type = ex.GetType().Name
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}