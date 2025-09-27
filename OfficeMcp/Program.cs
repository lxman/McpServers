using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using OfficeMcp.Services;
using OfficeMcp.Tools;

namespace OfficeMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        // CRITICAL: Redirect console output to prevent JSON-RPC corruption
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        // Add configuration
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
        
        // Configure services
        builder.Services
            .AddSingleton<OfficeService>()
            .AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Error);
                logging.AddFilter("Microsoft", LogLevel.None);
                logging.AddFilter("System", LogLevel.None);
            })
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "office-mcp",
                    Version = "1.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithTools<OfficeTools>();

        IHost host = builder.Build();
        await host.RunAsync();
    }
}