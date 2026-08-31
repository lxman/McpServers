using Mcp.Hosting.Core;
using Mcp.ResponseGuard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Serilog;
using SshClient.Core.Services;
using SshMcp.McpTools;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // The old SetupMcpLogging("SshMcp") call took a bare server name and resolved its own path;
    // McpHttpHost puts it under %LOCALAPPDATA%\McpServers\logs\ssh-mcp with every other server.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "ssh-mcp");

    Log.Information("Starting SSH MCP server");

    // Register core services
    builder.Services.AddSingleton<SshConnectionManager>();
    builder.Services.AddSingleton<SshCommandExecutor>();
    builder.Services.AddSingleton<SftpFileManager>();

    // Register response guard
    builder.Services.AddSingleton<OutputGuard>();

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<SshConnectionTools>()
        .WithTools<SshCommandTools>()
        .WithTools<SftpTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    // There was no try/catch here before, so a startup failure died silently with no log line.
    Log.Fatal(ex, "SSH MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
