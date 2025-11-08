using AzureMcp.McpTools;
using AzureServer.Core.Configuration;
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
        path: "logs/azure-mcp-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Azure MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Configure logging
    builder.Logging.ClearProviders();
    Console.SetOut(TextWriter.Null);
    Console.SetError(TextWriter.Null);
    builder.Logging.AddSerilog();

    // Register Azure Core services
    await builder.Services.AddAzureServicesWithPureDiscoveryAsync();

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
    Log.CloseAndFlush();
}