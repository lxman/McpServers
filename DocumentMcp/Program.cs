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
using SerilogFileWriter;

// Configure Serilog to write to a file (not stdout!)
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/document-mcp-.log");

try
{
    Log.Information("Starting Document MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

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
    await Log.CloseAndFlushAsync();
}