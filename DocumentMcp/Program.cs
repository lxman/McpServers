using DocumentMcp.McpTools;
using DocumentServer.Core.Services.Analysis;
using DocumentServer.Core.Services.Core;
using DocumentServer.Core.Services.DocumentSearch;
using DocumentServer.Core.Services.Lucene;
using DocumentServer.Core.Services.Ocr;
using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Services;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // The old "logs/document-mcp-.log" resolved against the working directory, which is a versioned
    // deploy directory now.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "document");

    Log.Information("Starting Document MCP server");

    // Register DocumentServer.Core services
    builder.Services.AddSingleton<DocumentCache>();
    builder.Services.AddSingleton<PasswordManager>(); // Must be registered before document loaders

    // Register document loaders (required by DocumentLoaderFactory)
    builder.Services.AddSingleton<IDocumentLoader, PdfDocumentLoader>();
    builder.Services.AddSingleton<IDocumentLoader, OfficeDocumentLoader>();

    // Register content extractors (required by DocumentProcessor)
    builder.Services.AddSingleton<IContentExtractor, PdfContentExtractor>();
    builder.Services.AddSingleton<IContentExtractor, OfficeContentExtractor>();

    builder.Services.AddSingleton<DocumentLoaderFactory>();
    builder.Services.AddSingleton<DocumentProcessor>();
    builder.Services.AddSingleton<DocumentValidator>();
    builder.Services.AddSingleton<DocumentComparator>();
    builder.Services.AddSingleton<MetadataExtractor>();

    // TesseractEngine and ImagePreprocessor must be registered before OcrService
    builder.Services.AddSingleton<TesseractEngine>();
    builder.Services.AddSingleton<ImagePreprocessor>();
    builder.Services.AddSingleton<OcrService>();

    // IndexManager must be registered before LuceneIndexer and LuceneSearcher
    builder.Services.AddSingleton<IndexManager>();
    builder.Services.AddSingleton<QuickSearchService>();
    builder.Services.AddSingleton<LuceneIndexer>();
    builder.Services.AddSingleton<LuceneSearcher>();

    // Register OutputGuard with a custom 15k token limit for document extraction operations
    builder.Services.AddSingleton(sp => new OutputGuard(
        sp.GetRequiredService<ILogger<OutputGuard>>(),
        new OutputGuardOptions { SafeTokenLimit = 15_000 }));

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<DocumentTools>()
        .WithTools<OcrTools>()
        .WithTools<IndexTools>()
        .WithTools<SearchTools>()
        .WithTools<PasswordTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Document MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}