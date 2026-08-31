using System.Net.Security;
using DesktopCommander.Core.Services;
using DesktopCommander.Core.Services.AdvancedFileEditing;
using DesktopCommanderMcp.McpTools;
using Mcp.ResponseGuard.Services;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // The old "logs/desktop-commander-.log" resolved against the working directory, which is a
    // versioned deploy directory now.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "desktop-commander");

    Log.Information("Starting Desktop Commander server.");

    // Add Memory Cache for ServerRegistry and other services
    builder.Services.AddMemoryCache();

    // Register DesktopCommander services
    builder.Services.AddSingleton<SecurityManager>();
    builder.Services.AddSingleton<AuditLogger>();
    builder.Services.AddSingleton<FileVersionService>();
    builder.Services.AddSingleton<ProcessManager>();
    builder.Services.AddSingleton<HexAnalysisService>();
    builder.Services.AddSingleton<OutputGuard>();

    // File editing services
    builder.Services.AddSingleton<EditApprovalService>();
    builder.Services.AddSingleton<FileEditor>();
    builder.Services.AddSingleton<LineBasedEditor>();
    builder.Services.AddSingleton<DiffPatchService>();
    builder.Services.AddSingleton<IndentationManager>();

    // Configure HttpClient for making requests to other MCP servers
    builder.Services.AddHttpClient("directory-client", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "DirectoryMcp/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Allow self-signed certificates for localhost development
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                // Only bypass certificate validation for localhost
                if (message.RequestUri?.Host is "localhost" or "127.0.0.1")
                {
                    return true;
                }

                // For all other hosts, use default validation
                return errors == SslPolicyErrors.None;
            }
        });

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<HttpTools>()
        // File System Operations
        .WithTools<FileSystemTools>()
        // Advanced File Operations
        .WithTools<AdvancedFileReadingTools>()
        .WithTools<FileEditingTools>()
        // Process and Terminal Management
        .WithTools<ProcessTools>()
        .WithTools<TerminalTools>()
        // Binary Analysis
        .WithTools<HexAnalysisTools>()
        // Configuration
        .WithTools<ConfigurationTools>()
        // Registry Management
        .WithTools<DesktopCommanderMcp.McpTools.RegistryTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DesktopCommander terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
