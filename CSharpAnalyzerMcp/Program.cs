using CSharpAnalyzerMcp.McpTools;
using CSharpAnalyzer.Core.Services.Reflection;
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
        path: "logs/csharpanalyzer-mcp-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting CSharp Analyzer MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Configure logging
    builder.Logging.ClearProviders();
    Console.SetOut(TextWriter.Null);
    Console.SetError(TextWriter.Null);
    builder.Logging.AddSerilog();

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
    Log.CloseAndFlush();
}