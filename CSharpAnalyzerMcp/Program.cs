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

    // Reflection services are SCOPED -- the MCP SDK resolves tool targets through a per-request
    // scope, so this is one loader per tool call. AssemblyLoaderService was registered nowhere at
    // all, which is why get_assembly_info and list_types threw on every call. Registering it as a
    // singleton would have fixed the throw and opened two worse holes: its cache is a plain
    // Dictionary with no synchronisation, and it holds a MetadataLoadContext, which keeps the
    // inspected .dll OPEN until disposed -- on this server's shared pool with a 30-minute idle
    // timeout that means locking the user's own build output, the exact failure this gateway
    // exists to remove. Scoped closes both: never shared across calls, and DI disposes it at the
    // end of each one. The cost is re-reading metadata per call, which is cheap.
    // AssemblyAnalysisService moves with it, or it would capture a scoped dependency.
    builder.Services.AddScoped<AssemblyLoaderService>();
    builder.Services.AddScoped<AssemblyAnalysisService>();

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
