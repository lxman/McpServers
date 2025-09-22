using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AzureMcp.Configuration;
using AzureMcp.Tools;
using AzureMcp.Resources;
using AzureMcp.Prompts;

namespace AzureMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        EnsureEnvironmentVariables();
        
        // Suppress console output for MCP protocol
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        var builder = Host.CreateApplicationBuilder(args);
        
        // Minimal configuration - only environment variables and command line
        builder.Configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        
        // Configure logging (minimal for MCP)
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Error);
            logging.AddFilter("Microsoft", LogLevel.None);
            logging.AddFilter("System", LogLevel.None);
            logging.AddFilter("Azure", LogLevel.None);
        });

        await builder.Services.AddAzureServicesWithPureDiscoveryAsync();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<DevOpsTools>()
            .WithResources<EmptyResourceProvider>()
            .WithPrompts<EmptyPromptProvider>();

        var host = builder.Build();
        await host.RunAsync();
    }
    
    private static void EnsureEnvironmentVariables()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATHEXT")))
        {
            Environment.SetEnvironmentVariable("PATHEXT", ".COM;.EXE;.BAT;.CMD;.VBS;.JS;.WS;.MSC");
        }
        
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
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