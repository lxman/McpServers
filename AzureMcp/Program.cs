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
        
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.Configuration
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        
        builder.Services
            .AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Error);
                logging.AddFilter("Microsoft", LogLevel.None);
                logging.AddFilter("System", LogLevel.None);
                logging.AddFilter("Azure", LogLevel.None);
            })
            .AddAzureServices(builder.Configuration)
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<DevOpsTools>()
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
