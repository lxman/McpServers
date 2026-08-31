using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Services;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using MongoMcp.McpTools;
using MongoServer.Core;
using MongoServer.Core.Services;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost,
    // which clears the logging providers and installs Serilog. The DisableDefaults dance that used
    // to keep console output off stdout went with the stdio transport that needed it.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "mongo");

    // Register MongoServer.Core services
    builder.Services.AddSingleton<MongoDbService>();
    builder.Services.AddSingleton<CrossServerOperations>(sp =>
    {
        var mongoService = sp.GetRequiredService<MongoDbService>();
        var logger = sp.GetRequiredService<ILogger<CrossServerOperations>>();
        return new CrossServerOperations(mongoService.ConnectionManager, logger);
    });

    // Register OutputGuard with custom 15k token limit for MongoDB query operations
    builder.Services.AddSingleton(sp => new OutputGuard(
        sp.GetRequiredService<ILogger<OutputGuard>>(),
        new OutputGuardOptions { SafeTokenLimit = 15_000 }));

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<ConnectionTools>()
        .WithTools<DatabaseTools>()
        .WithTools<CollectionTools>()
        .WithTools<AdvancedTools>()
        .WithTools<CrossServerTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    Log.Information("MongoMcp starting...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MongoMcp terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;