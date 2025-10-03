using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopDriver.Services;
using DesktopDriver.Services.AdvancedFileEditing;
using DesktopDriver.Services.DocumentSearching;
using DesktopDriver.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Suppress console output for clean MCP communication
Console.SetOut(TextWriter.Null);
Console.SetError(TextWriter.Null);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure JSON serialization options globally
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.MaxDepth = 128;
    options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.WriteIndented = true;
});

builder.Services
    // Services
    .AddSingleton<FileVersionService>()
    .AddSingleton<SecurityManager>()
    .AddSingleton<AuditLogger>()
    .AddSingleton<ProcessManager>()
    .AddSingleton<PasswordManager>()
    .AddSingleton<OcrService>()
    .AddSingleton<DocumentProcessor>()
    .AddSingleton<DocumentIndexer>()
    .AddSingleton<DiffPatchService>()
    .AddSingleton<IndentationManager>()
    .AddSingleton<LineBasedEditor>()
    .AddSingleton<FileEditor>()
    .AddSingleton<LineBasedEditor>()
    .AddSingleton<HexAnalysisService>()
    
    // Tools
    .AddSingleton<AdvancedFileEditingTools>()
    .AddSingleton<AdvancedFileReadingTools>()
    .AddSingleton<TerminalTools>()
    .AddSingleton<FileSystemTools>()
    .AddSingleton<ProcessTools>()
    .AddSingleton<ConfigurationTools>()
    .AddSingleton<DocTools>()
    .AddSingleton<ConfigurationTools>()
    .AddSingleton<DocTools>()
    .AddSingleton<HexAnalysisTools>()
    
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
    .WithTools<AdvancedFileEditingTools>()
    .WithTools<TerminalTools>()
    .WithTools<FileSystemTools>()
    .WithTools<ProcessTools>()
    .WithTools<ConfigurationTools>()
    .WithTools<DocTools>()
    .WithTools<HexAnalysisTools>();

IHost host = builder.Build();
await host.RunAsync();
