using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

namespace McpUtilitiesServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/mcp-utilities-.log");

        try
        {
            Log.Information("Starting Utilities server.");

            // Create application builder with proper logging setup
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: false);

            // Configure the MCP server with proper tool registration
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<TimeUtilities>();

            IHost host = builder.Build();

            await Console.Error.WriteLineAsync("MCP time utilities server ready to handle requests");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Utilities server terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
