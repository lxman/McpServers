using CodeAssist.Core.Extensions;
using CodeAssistMcp.McpTools;
using CodeAssistMcp.Services;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in
    // McpHttpHost. Log.Logger is configured by the time this returns.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "code-assist");

    Log.Information("Starting CodeAssist MCP server");

    builder.Services.AddCodeAssistServices(builder.Configuration);
    builder.Services.AddSingleton<RepositoryWatcherStartupService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RepositoryWatcherStartupService>());

    builder.Services.AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<HealthTools>()
        .WithTools<IndexTools>()
        .WithTools<SearchTools>()
        .WithTools<RepositoryTools>()
        .WithTools<PersonalContextTools>()
        .WithTools<DataFlowTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CodeAssist MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
