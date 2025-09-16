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
using AwsMcp.Resources;
using AwsMcp.Prompts;

namespace AwsMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        EnsureEnvironmentVariables();
        
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.Configuration
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        
        builder.Services
            .AddSingleton<S3Service>()
            .AddSingleton<CloudWatchService>()
            .AddSingleton<EcsService>()
            .AddSingleton<EcrService>()
            .AddSingleton<AwsDiscoveryService>()
            .AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Error);
                logging.AddFilter("Microsoft", LogLevel.None);
                logging.AddFilter("System", LogLevel.None);
                logging.AddFilter("Amazon", LogLevel.None);
                logging.AddFilter("AWS", LogLevel.None);
            })
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<S3Tools>()
            .WithTools<CloudWatchTools>()
            .WithTools<EcsTools>()
            .WithTools<EcrTools>()
            .WithTools<AwsDiscoveryTools>()
            .WithResources<EmptyResourceProvider>()
            .WithPrompts<EmptyPromptProvider>();

        IHost host = builder.Build();
        
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