using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DesktopDriver.Services;
using DesktopDriver.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    .AddSingleton<SecurityManager>()
    .AddSingleton<AuditLogger>()
    .AddSingleton<ProcessManager>()
    .AddSingleton<TerminalTools>()
    .AddSingleton<FileSystemTools>()
    .AddSingleton<ProcessTools>()
    .AddSingleton<ConfigurationTools>()
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
    .WithTools<ConfigurationTools>();

IHost host = builder.Build();
await host.RunAsync();
