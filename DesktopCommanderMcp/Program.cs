using System.Net.Security;
using DesktopCommander.Core.Services;
using DesktopCommander.Core.Services.AdvancedFileEditing;
using DesktopCommanderMcp.McpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();

// Add Memory Cache for ServerRegistry and other services
builder.Services.AddMemoryCache();

// Register DesktopCommander services
builder.Services.AddSingleton<SecurityManager>();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<FileVersionService>();
builder.Services.AddSingleton<ProcessManager>();
builder.Services.AddSingleton<HexAnalysisService>();
builder.Services.AddSingleton<ResponseSizeGuard>();

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

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
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
    .WithTools<ConfigurationTools>();

var host = builder.Build();

await host.RunAsync();