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

    /// <summary>%LOCALAPPDATA%\McpServers\data\&lt;name&gt;</summary>
    public static string DataPathFor(string serverName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpServers", "data", serverName);

    /// <summary>
    /// Turns whatever a server has configured as its data directory into an absolute path.
    /// <para>
    /// The reason this exists is the same one that moved logging in here. A converted server runs
    /// from a VERSIONED deploy directory, so a relative path -- edgar shipped "./data" -- no longer
    /// means what it meant under stdio: it now resolves under the current version's directory and
    /// moves again at the next deploy, orphaning everything written under the previous one. Data
    /// loss, silently, and only visible long after the deploy that caused it.
    /// </para>
    /// <para>
    /// An absolute path is honoured as configured -- that is the escape hatch for a user who wants
    /// their data somewhere specific. Anything else is anchored to the server's own data root.
    /// </para>
    /// </summary>
    public static string ResolveDataDirectory(string? configured, string serverName)
    {
        string root = DataPathFor(serverName);

        if (string.IsNullOrWhiteSpace(configured)) return root;

        // One expression covers both remaining cases, because Path.Combine returns its second
        // argument unchanged when that argument is already rooted -- so a configured absolute path
        // wins and a relative one is anchored to the server's root. An explicit IsPathRooted branch
        // ahead of this was tried and deleted: no mutation of it could fail a test, because it only
        // restated what Path.Combine already does.
        //
        // GetFullPath is doing real work either way: "./data" would otherwise keep its leading
        // "./" and compare unequal to the same directory spelled plainly.
        return Path.GetFullPath(Path.Combine(root, configured));
    }

    private static McpHostOptions ReadOptions(string[] args, string serverName) => new()
    {
        ServerName = Environment.GetEnvironmentVariable("MCP_SERVER_NAME") ?? serverName,
        PortFilePath = ReadArg(args, "--mcp-port-file"),

        // MCP_SHUTDOWN_TOKEN is the historical name -- it once guarded /admin/shutdown alone. It
        // now authenticates every endpoint this backend serves, so it is read into AuthToken. The
        // variable name is kept so a version directory published before the widening still gets
        // its token from a newer gateway.
        AuthToken = Environment.GetEnvironmentVariable("MCP_SHUTDOWN_TOKEN"),
        Version = Environment.GetEnvironmentVariable("MCP_SERVER_VERSION") ?? "unknown"
    };

    private static string? ReadArg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
