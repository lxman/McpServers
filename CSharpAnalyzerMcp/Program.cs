using CSharpAnalyzerMcp.McpTools;
using CSharpAnalyzer.Core.Services.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (not stdout!)
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/csharpanalyzer-mcp-.log");

try
{
    Log.Information("Starting CSharp Analyzer MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Register CSharpAnalyzer.Core services
    builder.Services.AddSingleton<AssemblyAnalysisService>();

    // Configure MCP server with STDIO transport
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<RoslynTools>()
        .WithTools<ReflectionTools>();

    IHost host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CSharp Analyzer MCP server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}