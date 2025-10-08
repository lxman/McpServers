using AzureMcp.Configuration;
using AzureMcp.Prompts;
using AzureMcp.Resources;
using AzureMcp.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        EnsureEnvironmentVariables();
        
        // Suppress console output for MCP protocol
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        // Minimal configuration - only environment variables and command line
        builder.Configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
    
            // Add file logging to the project directory (not blocked)
            string logPath = Path.Combine(AppContext.BaseDirectory, "azure-discovery.log");
            logging.AddFile(logPath, LogLevel.Debug);
    
            // Keep minimal logging for MCP protocol
            logging.SetMinimumLevel(LogLevel.Error);
    
            // But allow debug logging for our discovery classes
            logging.AddFilter("AzureMcp.Authentication", LogLevel.Debug);
    
            // Suppress framework noise
            logging.AddFilter("Microsoft", LogLevel.None);
            logging.AddFilter("System", LogLevel.None);
            logging.AddFilter("Azure", LogLevel.None);
        });

        await builder.Services.AddAzureServicesWithPureDiscoveryAsync();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<DevOpsTools>()
            .WithTools<ResourceManagementTools>()
            .WithTools<CredentialManagementTools>()
            .WithTools<CostManagementTools>()
            .WithTools<FileStorageTools>()
            .WithTools<StorageTools>()
            .WithTools<KeyVaultTools>()
            .WithTools<SqlTools>()
            .WithTools<AppServiceTools>()
            .WithTools<MonitorTools>()
            .WithTools<NetworkingTools>()
            .WithResources<EmptyResourceProvider>()
            .WithTools<EntraAuthTools>()
            .WithPrompts<EmptyPromptProvider>();


        IHost host = builder.Build();
        await host.RunAsync();
    }
    
    private static void EnsureEnvironmentVariables()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATHEXT")))
        {
            Environment.SetEnvironmentVariable("PATHEXT", ".COM;.EXE;.BAT;.CMD;.VBS;.JS;.WS;.MSC");
        }
        
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        const string dotnetPath = @"C:\Program Files\dotnet";
        if (!path.Contains(dotnetPath, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("PATH", $"{path};{dotnetPath}");
        }
        
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", @"C:\Program Files\dotnet");
        }
        
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        Environment.SetEnvironmentVariable("ASPNETCORE_HOSTING_MODEL", "InProcess");
    }
}