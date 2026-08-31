using CSharpAnalyzerMcp.McpTools;
using CSharpAnalyzer.Core.Services.Reflection;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in
    // McpHttpHost. Log.Logger is configured by the time this returns, so the old
    // SetupMcpLogging call and its relative "logs/" path are gone -- that path resolved against
    // the working directory, which is now a versioned deploy directory.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "csharp-analyzer");

    Log.Information("Starting CSharp Analyzer MCP server");

    // Register CSharpAnalyzer.Core services
    builder.Services.AddSingleton<AssemblyAnalysisService>();

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<RoslynTools>()
        .WithTools<ReflectionTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CSharp Analyzer MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
