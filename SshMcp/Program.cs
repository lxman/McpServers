using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mcp.ResponseGuard;
using Mcp.ResponseGuard.Services;
using Serilog;
using SerilogFileWriter;
using SshClient.Core.Services;
using SshMcp.McpTools;

// Setup MCP-safe logging (redirects Console to file)
McpLoggingExtensions.SetupMcpLogging("SshMcp");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Register core services
builder.Services.AddSingleton<SshConnectionManager>();
builder.Services.AddSingleton<SshCommandExecutor>();
builder.Services.AddSingleton<SftpFileManager>();

// Register response guard
builder.Services.AddSingleton<OutputGuard>();

// Add MCP server with all tool types
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SshConnectionTools>()
    .WithTools<SshCommandTools>()
    .WithTools<SftpTools>();

IHost app = builder.Build();

await app.RunAsync();
