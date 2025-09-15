using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AwsMcp.S3;
using AwsMcp.CloudWatch;
using AwsMcp.Configuration;
using AwsMcp.ECS;
using AwsMcp.ECR;
using AwsMcp.Tools;

namespace AwsMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        // CRITICAL: Ensure required environment variables are set before anything else
        EnsureEnvironmentVariables();
        
        // CRITICAL: Redirect all console output to prevent JSON-RPC corruption
        // AWS SDK and other components can write warnings to stdout
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        // Create the host builder with configuration
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        // Add configuration sources
        // CRITICAL: Use an absolute path to appsettings.json since the working directory differs in MCP
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.Configuration
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        
        // Configure services
        builder.Services
            // Add AWS services
            .AddSingleton<S3Service>()
            .AddSingleton<CloudWatchService>()
            .AddSingleton<EcsService>()
            .AddSingleton<EcrService>()
            .AddSingleton<AwsDiscoveryService>()  // Added Phase 2 Discovery Service
            // CRITICAL: Completely disable all logging to console/stdout
            .AddLogging(logging =>
            {
                // Clear all default providers that might write to the console
                logging.ClearProviders();
                
                // Only add a debug provider (goes to debugger, not console)
                logging.AddDebug();
                
                // Set minimum level to Error to reduce noise
                logging.SetMinimumLevel(LogLevel.Error);
                
                // Explicitly filter out all console logging
                logging.AddFilter("Microsoft", LogLevel.None);
                logging.AddFilter("System", LogLevel.None);
                logging.AddFilter("Amazon", LogLevel.None);
                logging.AddFilter("AWS", LogLevel.None);
            })
            // Add MCP Server with stdio transport
            .AddMcpServer()
            .WithStdioServerTransport()
            // Register all AWS tools
            .WithTools<S3Tools>()
            .WithTools<CloudWatchTools>()
            .WithTools<EcsTools>()
            .WithTools<EcrTools>()
            .WithTools<AwsDiscoveryTools>();  // Added Phase 2 Discovery Tools

        // Build and run the host
        IHost host = builder.Build();
        
        // Start the MCP server
        await host.RunAsync();
    }
    
    /// <summary>
    /// Ensures required environment variables are set to prevent .NET CLI issues
    /// </summary>
    private static void EnsureEnvironmentVariables()
    {
        // Ensure PATHEXT is set (critical for Windows .NET CLI)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATHEXT")))
        {
            Environment.SetEnvironmentVariable("PATHEXT", ".COM;.EXE;.BAT;.CMD;.VBS;.JS;.WS;.MSC");
        }
        
        // Ensure the PATH includes a .NET directory
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        const string dotnetPath = @"C:\Program Files\dotnet";
        if (!path.Contains(dotnetPath, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("PATH", $"{path};{dotnetPath}");
        }
        
        // Set additional environment variables that .NET CLI might need
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", @"C:\Program Files\dotnet");
        }
        
        // Disable .NET CLI telemetry to reduce potential issues
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        
        // Set the .NET hosting model to in-process to avoid CLI issues
        Environment.SetEnvironmentVariable("ASPNETCORE_HOSTING_MODEL", "InProcess");
    }
}