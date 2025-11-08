using DocumentMcp.McpTools;
using DocumentServer.Core.Services.Analysis;
using DocumentServer.Core.Services.Core;
using DocumentServer.Core.Services.DocumentSearch;
using DocumentServer.Core.Services.Lucene;
using DocumentServer.Core.Services.Ocr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// Configure Serilog to write to a file (not stdout!)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/document-mcp-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Document MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Configure logging
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    // Register DocumentServer.Core services
    builder.Services.AddSingleton<DocumentCache>();
    builder.Services.AddSingleton<DocumentLoaderFactory>();
    builder.Services.AddSingleton<DocumentProcessor>();
    builder.Services.AddSingleton<DocumentValidator>();
    builder.Services.AddSingleton<DocumentComparator>();
    builder.Services.AddSingleton<MetadataExtractor>();
    builder.Services.AddSingleton<OcrService>();
    builder.Services.AddSingleton<TesseractEngine>();
    builder.Services.AddSingleton<ImagePreprocessor>();
    builder.Services.AddSingleton<PasswordManager>();
    builder.Services.AddSingleton<QuickSearchService>();
    builder.Services.AddSingleton<LuceneIndexer>();
    builder.Services.AddSingleton<LuceneSearcher>();
    builder.Services.AddSingleton<IndexManager>();

    // Configure MCP server with STDIO transport
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<DocumentTools>()
        .WithTools<OcrTools>()
        .WithTools<IndexTools>()
        .WithTools<SearchTools>()
        .WithTools<PasswordTools>();

    IHost host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Document MCP server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}