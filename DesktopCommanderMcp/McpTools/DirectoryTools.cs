using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Models;
using DesktopCommanderMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for discovering and browsing available MCP servers
/// </summary>
[McpServerToolType]
public class DirectoryTools(ServerRegistry registry, ILogger<DirectoryTools> logger)
{
    [McpServerTool, DisplayName("list_servers")]
    [Description("Get directory of MCP server APIs and status. See service-management/SKILL.md")]
    public string ListServers()
    {
        logger.LogInformation("list_servers called");

        try
        {
            ServerConfiguration config = registry.GetConfiguration();
            var result = new Dictionary<string, object>
            {
                ["servers"] = config.Servers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        name = kvp.Value.Name,
                        url = kvp.Value.Url,
                        port = kvp.Value.Port,
                        status = IsServerRunning(kvp.Value.Port) ? "running" : "stopped"
                    }
                ),
                ["usage"] = config.Usage ?? "Call GET {url}/description to see available endpoints and capabilities."
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in list_servers");
            return JsonSerializer.Serialize(new { error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    private static bool IsServerRunning(int? port)
    {
        if (!port.HasValue) return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = $"-ano | findstr :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null) return false;

            string output = process.StandardOutput.ReadToEnd();
            return output.Contains("LISTENING");
        }
        catch
        {
            return false;
        }
    }
}