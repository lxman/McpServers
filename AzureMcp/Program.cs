using AzureMcp.McpTools;
using AzureServer.Core.Configuration;
using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (not stdout!)
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/azure-mcp-.log");

try
{
    Log.Information("Starting Azure MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Register Azure Core services
    await builder.Services.AddAzureServicesWithPureDiscoveryAsync();

    // Register OutputGuard with custom 15k token limit for Azure Monitor log operations
    builder.Services.AddSingleton(sp => new OutputGuard(
        sp.GetRequiredService<ILogger<OutputGuard>>(),
        new OutputGuardOptions { SafeTokenLimit = 15_000 }));

    // Configure MCP server with STDIO transport
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
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

    IHost host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Azure MCP server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}