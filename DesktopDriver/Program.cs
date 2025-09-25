using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopDriver.Services;
using DesktopDriver.Services.Doc;
using DesktopDriver.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Suppress console output for clean MCP communication
Console.SetOut(TextWriter.Null);
Console.SetError(TextWriter.Null);

var builder = Host.CreateApplicationBuilder(args);

// Configure JSON serialization options globally
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.MaxDepth = 128;
    options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.WriteIndented = true;
});

builder.Services
    // Existing services
    .AddSingleton<SecurityManager>()
    .AddSingleton<AuditLogger>()
    .AddSingleton<ProcessManager>()
    
    // Document processing services
    .AddSingleton<PasswordManager>()
    .AddSingleton<DocumentProcessor>()
    .AddSingleton<DocumentIndexer>()
    
    // Tools
    .AddSingleton<TerminalTools>()
    .AddSingleton<FileSystemTools>()
    .AddSingleton<ProcessTools>()
    .AddSingleton<ConfigurationTools>()
    .AddSingleton<DocTools>()
    
    .AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Error);

        // Suppress noisy framework logs
        logging.AddFilter("Microsoft", LogLevel.None);
        logging.AddFilter("System", LogLevel.None);
    })
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TerminalTools>()
    .WithTools<FileSystemTools>()
    .WithTools<ProcessTools>()
    .WithTools<ConfigurationTools>()
    .WithTools<DocTools>();

var host = builder.Build();
await host.RunAsync();