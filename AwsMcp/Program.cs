using AwsMcp.McpTools;
using AwsServer.Core.Configuration;
using Mcp.Hosting.Core;
using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // The old "logs/aws-mcp-.log" resolved against the working directory, which is a versioned
    // deploy directory now.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "aws");

    Log.Information("Starting AWS MCP server");

    // Register AWS Core services
    builder.Services.AddAwsServices();

    // Register OutputGuard with a custom 15k token limit for AWS CloudWatch log operations
    builder.Services.AddSingleton(sp => new OutputGuard(
        sp.GetRequiredService<ILogger<OutputGuard>>(),
        new OutputGuardOptions { SafeTokenLimit = 15_000 }));

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        // AWS Service Tools
        .WithTools<S3Tools>()
        .WithTools<CloudWatchTools>()
        .WithTools<EcsTools>()
        .WithTools<EcrTools>()
        .WithTools<QuickSightTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AWS MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
