using AzureMcp.McpTools;
using AzureServer.Core.Configuration;
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
    // The old "logs/azure-mcp-.log" resolved against the working directory, which is a versioned
    // deploy directory now.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "azure");

    Log.Information("Starting Azure MCP server");

    // Register Azure Core services
    await builder.Services.AddAzureServicesWithPureDiscoveryAsync();

    // Register OutputGuard with custom 15k token limit for Azure Monitor log operations
    builder.Services.AddSingleton(sp => new OutputGuard(
        sp.GetRequiredService<ILogger<OutputGuard>>(),
        new OutputGuardOptions { SafeTokenLimit = 15_000 }));

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        // Azure Service Tools
        .WithTools<HealthTools>()
        .WithTools<StorageTools>()
        .WithTools<FileStorageTools>()
        .WithTools<AppServiceTools>()
        .WithTools<ContainerTools>()
        .WithTools<KeyVaultTools>()
        .WithTools<MonitorTools>()
        .WithTools<SqlTools>()
        .WithTools<ServiceBusTools>()
        .WithTools<EventHubsTools>()
        .WithTools<NetworkingTools>()
        .WithTools<ResourceManagementTools>()
        .WithTools<CostManagementTools>()
        .WithTools<DevOpsTools>()
        .WithTools<CredentialManagementTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Azure MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}