using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AwsMcp.S3;
using AwsMcp.CloudWatch;
using AwsMcp.ECS;
using AwsMcp.ECR;
using AwsMcp.Tools;

namespace AwsMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        // CRITICAL: Redirect all console output to prevent JSON-RPC corruption
        // AWS SDK and other components can write warnings to stdout
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        // Create the host builder with configuration
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        // Add configuration sources
        // CRITICAL: Use absolute path to appsettings.json since working directory differs in MCP
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
            // CRITICAL: Completely disable all logging to console/stdout
            .AddLogging(logging =>
            {
                // Clear all default providers that might write to console
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
            .WithTools<EcrTools>();

        // Build and run the host
        IHost host = builder.Build();
        
        // Start the MCP server
        await host.RunAsync();
    }
}
