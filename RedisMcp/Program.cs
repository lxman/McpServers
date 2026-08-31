using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Mcp.Database.Core.Redis;
using ModelContextProtocol.AspNetCore;
using RedisBrowser.Core.Services;
using RedisMcp.McpTools;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // The old log path was built from AppContext.BaseDirectory, which is a versioned deploy
    // directory now, so logs would have scattered one directory per deploy.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "redis");

    Log.Information("Starting RedisMcp");

    // Register Redis connection manager
    builder.Services.AddRedisConnectionManager();

    // Register RedisBrowser.Core services
    builder.Services.AddSingleton<RedisService>();

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<ConnectionTools>()
        .WithTools<KeyTools>()
        .WithTools<ExpiryTools>()
        .WithTools<ServerTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    Log.Information("RedisMcp starting...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RedisMcp terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
