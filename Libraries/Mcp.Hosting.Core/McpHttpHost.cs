using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

namespace Mcp.Hosting.Core;

public static class McpHttpHost
{
    /// <summary>
    /// The host shape every converted MCP server uses: loopback Kestrel on an OS-assigned port,
    /// logs at an absolute per-server path, and the options the gateway passes in.
    /// </summary>
    public static WebApplicationBuilder CreateBuilder(string[] args, string serverName)
    {
        McpHostOptions options = ReadOptions(args, serverName);

        Log.Logger = McpLoggingExtensions.SetupMcpLogging(LogPathFor(options.ServerName));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpContextAccessor();

        return builder;
    }

    /// <summary>%LOCALAPPDATA%\McpServers\logs\&lt;name&gt;\&lt;name&gt;-.log</summary>
    public static string LogPathFor(string serverName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpServers", "logs", serverName, $"{serverName}-.log");

    private static McpHostOptions ReadOptions(string[] args, string serverName) => new()
    {
        ServerName = Environment.GetEnvironmentVariable("MCP_SERVER_NAME") ?? serverName,
        PortFilePath = ReadArg(args, "--mcp-port-file"),
        ShutdownToken = Environment.GetEnvironmentVariable("MCP_SHUTDOWN_TOKEN"),
        Version = Environment.GetEnvironmentVariable("MCP_SERVER_VERSION") ?? "unknown"
    };

    private static string? ReadArg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
